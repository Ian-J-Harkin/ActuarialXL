using System.Threading.Channels;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.API.Services;

public interface ITranslationJobQueue
{
    ValueTask EnqueueJobAsync(TranslationJobRequest request, CancellationToken cancellationToken = default);
    ValueTask<TranslationJobRequest> DequeueJobAsync(CancellationToken cancellationToken = default);
}

public class TranslationJobQueue : ITranslationJobQueue
{
    private readonly Channel<TranslationJobRequest> _queue;

    public TranslationJobQueue()
    {
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<TranslationJobRequest>(options);
    }

    public async ValueTask EnqueueJobAsync(TranslationJobRequest request, CancellationToken cancellationToken = default)
    {
        await _queue.Writer.WriteAsync(request, cancellationToken);
    }

    public async ValueTask<TranslationJobRequest> DequeueJobAsync(CancellationToken cancellationToken = default)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
