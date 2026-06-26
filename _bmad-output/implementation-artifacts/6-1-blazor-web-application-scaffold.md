# Story 6.1: Blazor Web Application Scaffold

**Status:** ready-for-dev

## Story Foundation
As an Actuary,
I want a web interface to interact with the translation engine,
So that I don't have to use API testing tools to upload files or review SQLite results.

### Acceptance Criteria:
**Given** the running ASP.NET Core WebAPI,
**When** a user navigates to the new Blazor application,
**Then** they must be presented with a file upload dashboard capable of handling `.xlsx` and `.xlsm` files,
**And** the application must successfully route the file to the WebAPI and display the returned `TranslationOutput` visually.

## Pre-Flight Verification & Technical Context
- **Project Structure:** A new `.NET` project named `ActuarialTranslationEngine.Web` must be created.
- **Decision Required:** The epic indicates "Blazor WASM / Server". The implementation plan must resolve this architectural ambiguity before execution.
- **API Connectivity:** Must communicate with `ActuarialTranslationEngine.API` over HTTP.
- **Aesthetics:** Must follow the Web Application Development rules (rich aesthetics, vibrant colors, dynamic design).
