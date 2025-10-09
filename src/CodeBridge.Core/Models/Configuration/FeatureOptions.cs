namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Configuration for optional SDK features.
/// </summary>
public class FeatureOptions
{
    /// <summary>
    /// Generate client-side validation using Zod schemas.
    /// </summary>
    public bool IncludeValidation { get; set; } = true;

    /// <summary>
    /// Generate authentication helper methods.
    /// </summary>
    public bool IncludeAuthentication { get; set; } = true;

    /// <summary>
    /// Generate GraphQL client (future support).
    /// </summary>
    public bool IncludeGraphQL { get; set; } = false;

    /// <summary>
    /// Generate React hooks (useQuery, useMutation).
    /// </summary>
    public bool GenerateReactHooks { get; set; } = true;

    /// <summary>
    /// Generate Next.js helpers (Server Components, API routes).
    /// </summary>
    public bool GenerateNextJsHelpers { get; set; } = false;

    /// <summary>
    /// Generate JSDoc comments for better IDE support.
    /// </summary>
    public bool GenerateDocComments { get; set; } = true;

    /// <summary>
    /// Generate README file with usage examples.
    /// </summary>
    public bool GenerateReadme { get; set; } = true;

    /// <summary>
    /// Include CSRF token support in HTTP client.
    /// </summary>
    public bool IncludeCsrf { get; set; } = true;

    /// <summary>
    /// Include file upload/download operations.
    /// </summary>
    public bool IncludeFileOperations { get; set; } = true;
}
