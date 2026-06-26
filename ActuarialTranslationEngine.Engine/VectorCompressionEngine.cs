using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Engine;

public class VectorCompressionEngine : IVectorCompressionEngine
{
    private static readonly Regex CellReferenceRegex = new Regex(@"(?<absCol>\$)?(?<col>[A-Za-z]+)(?<absRow>\$)?(?<row>[0-9]+)", RegexOptions.Compiled);

    public CompressedVectorBlock CompressTopology(RawWorkbookMap sourceMap)
    {
        if (sourceMap == null) throw new ArgumentNullException(nameof(sourceMap));
        if (sourceMap.DataRows == null || sourceMap.DataRows.Count == 0) return new CompressedVectorBlock { TargetWorksheet = sourceMap.SheetName ?? string.Empty };

        string archetype = "Time_Series_Roll_Forward";
        if (sourceMap.SheetName != null)
        {
            if (sourceMap.SheetName.Contains("16.3", StringComparison.OrdinalIgnoreCase)) archetype = "Stochastic_Modeling";
            else if (sourceMap.SheetName.Contains("18.4", StringComparison.OrdinalIgnoreCase)) archetype = "Multi_Component_Balancing_Ledger";
            else if (sourceMap.SheetName.Contains("13.12", StringComparison.OrdinalIgnoreCase)) archetype = "Variable_Payout_Adjuster";
        }

        var block = new CompressedVectorBlock
        {
            TargetWorksheet = sourceMap.SheetName,
            ProcessingArchetype = archetype
        };

        VectorRangePartition currentPartition = null;

        foreach (var row in sourceMap.DataRows.OrderBy(r => r.RowIndex))
        {
            if (row.DisruptiveNodes != null && row.DisruptiveNodes.Any())
            {
                block.DisruptiveNodes.AddRange(row.DisruptiveNodes);
            }

            var rowSignatureList = new List<string>();
            var rowColumns = new List<ColumnDefinition>();

            foreach (var header in sourceMap.Headers)
            {
                var rawFormula = row.CellFormulas.TryGetValue(header.ColumnLetter, out var f) ? f : string.Empty;
                var (tokenized, lookbacks) = AbstractFormula(rawFormula, row.RowIndex);

                rowSignatureList.Add($"{header.ColumnLetter}:{tokenized}");

                rowColumns.Add(new ColumnDefinition
                {
                    ColumnLetter = header.ColumnLetter,
                    ExtractedHeaderName = header.ExtractedHeaderName,
                    TokenizedFormulaTemplate = tokenized,
                    ChronologicalLookbacks = lookbacks
                });
            }

            var fullRowSignature = string.Join("|", rowSignatureList);

            if (currentPartition == null || currentPartition.FormulaSignature != fullRowSignature)
            {
                // Start a new partition
                currentPartition = new VectorRangePartition
                {
                    StartRow = row.RowIndex,
                    EndRow = row.RowIndex,
                    FormulaSignature = fullRowSignature,
                    StructuralColumns = rowColumns
                };
                block.Partitions.Add(currentPartition);
            }
            else
            {
                // Extend current partition
                currentPartition.EndRow = row.RowIndex;
            }
        }

        return block;
    }

    private (string tokenized, List<int> lookbacks) AbstractFormula(string formula, int currentRow)
    {
        if (string.IsNullOrWhiteSpace(formula)) return ("", new List<int>());

        var lookbacks = new HashSet<int>();
        var tokenized = CellReferenceRegex.Replace(formula, match =>
        {
            var absRow = match.Groups["absRow"].Success;
            if (absRow) return match.Value; // Absolute row reference, do not offset

            var col = match.Groups["col"].Value;
            var rowStr = match.Groups["row"].Value;
            
            if (int.TryParse(rowStr, out var rowNum))
            {
                var offset = rowNum - currentRow;
                if (offset < 0)
                {
                    lookbacks.Add(offset);
                    return $"Col[{col}][{offset}]";
                }
                else if (offset > 0)
                {
                    lookbacks.Add(offset);
                    return $"Col[{col}][+{offset}]";
                }
                else
                {
                    return $"Col[{col}]";
                }
            }
            return match.Value;
        });

        return (tokenized, lookbacks.OrderBy(l => l).ToList());
    }
}
