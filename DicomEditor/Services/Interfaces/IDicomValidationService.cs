using FellowOakDicom;

namespace DicomEditor.Services.Interfaces;

/// <summary>
/// Service interface for validating DICOM tag values according to DICOM standards.
/// </summary>
public interface IDicomValidationService
{
    /// <summary>
    /// Validates a value for a specific tag based on its VR.
    /// </summary>
    ValidationResult ValidateValue(DicomTag tag, string vr, string value);

    /// <summary>
    /// Validates an entire dataset for conformance.
    /// </summary>
    IEnumerable<ValidationResult> ValidateDataset(FellowOakDicom.DicomDataset dataset);

    /// <summary>
    /// Gets the maximum length for a VR type.
    /// </summary>
    int? GetMaxLength(string vr);

    /// <summary>
    /// Checks if a value is valid for the specified VR.
    /// </summary>
    bool IsValidForVR(string vr, string value);
}

/// <summary>
/// Result of a validation operation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }
    public DicomTag? Tag { get; }
    public ValidationSeverity Severity { get; }

    private ValidationResult(bool isValid, string? errorMessage, DicomTag? tag, ValidationSeverity severity)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
        Tag = tag;
        Severity = severity;
    }

    public static ValidationResult Success() => new(true, null, null, ValidationSeverity.None);
    public static ValidationResult Error(string message, DicomTag? tag = null) 
        => new(false, message, tag, ValidationSeverity.Error);
    public static ValidationResult Warning(string message, DicomTag? tag = null) 
        => new(true, message, tag, ValidationSeverity.Warning);
}

public enum ValidationSeverity
{
    None,
    Warning,
    Error
}
