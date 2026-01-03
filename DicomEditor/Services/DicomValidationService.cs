using DicomEditor.Services.Interfaces;
using FellowOakDicom;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DicomEditor.Services;

/// <summary>
/// Implementation of IDicomValidationService.
/// Validates DICOM tag values according to DICOM standards and VR constraints.
/// </summary>
public partial class DicomValidationService : IDicomValidationService
{
    private readonly ILogger<DicomValidationService> _logger;

    // VR maximum lengths per DICOM standard (per single value, not multi-valued)
    private static readonly Dictionary<string, int> VRMaxLengths = new()
    {
        ["AE"] = 16,    // Application Entity
        ["AS"] = 4,     // Age String
        ["AT"] = 4,     // Attribute Tag
        ["CS"] = 16,    // Code String (per value in multi-valued string)
        ["DA"] = 8,     // Date
        ["DS"] = 16,    // Decimal String
        ["DT"] = 26,    // Date Time
        ["FL"] = 4,     // Floating Point Single
        ["FD"] = 8,     // Floating Point Double
        ["IS"] = 12,    // Integer String
        ["LO"] = 64,    // Long String
        ["LT"] = 10240, // Long Text
        ["PN"] = 64,    // Person Name (per component)
        ["SH"] = 16,    // Short String
        ["SL"] = 4,     // Signed Long
        ["SS"] = 2,     // Signed Short
        ["ST"] = 1024,  // Short Text
        ["TM"] = 14,    // Time
        ["UC"] = -1,    // Unlimited Characters (no limit)
        ["UI"] = 64,    // Unique Identifier
        ["UL"] = 4,     // Unsigned Long
        ["UN"] = -1,    // Unknown (no limit)
        ["UR"] = -1,    // URI (no limit)
        ["US"] = 2,     // Unsigned Short
        ["UT"] = -1,    // Unlimited Text (no limit)
    };

    // VRs that support multi-valued strings (backslash-separated)
    private static readonly HashSet<string> MultiValuedVRs = new()
    {
        "AE", "AS", "CS", "DA", "DS", "DT", "IS", "LO", "PN", "SH", "TM", "UI"
    };

    public DicomValidationService(ILogger<DicomValidationService> logger)
    {
        _logger = logger;
    }

    public ValidationResult ValidateValue(DicomTag tag, string vr, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return ValidationResult.Success(); // Empty values are generally allowed
        }

        // For multi-valued VRs, validate each component separately
        if (MultiValuedVRs.Contains(vr) && value.Contains('\\'))
        {
            return ValidateMultiValuedString(tag, vr, value);
        }

        // Check length constraint for single values
        var maxLength = GetMaxLength(vr);
        if (maxLength > 0 && value.Length > maxLength)
        {
            return ValidationResult.Error(
                $"Value exceeds maximum length of {maxLength} characters for VR {vr}",
                tag);
        }

        // VR-specific validation
        return vr switch
        {
            "AE" => ValidateAE(value, tag),
            "AS" => ValidateAS(value, tag),
            "CS" => ValidateCS(value, tag),
            "DA" => ValidateDA(value, tag),
            "DS" => ValidateDS(value, tag),
            "DT" => ValidateDT(value, tag),
            "IS" => ValidateIS(value, tag),
            "LO" => ValidateLO(value, tag),
            "PN" => ValidatePN(value, tag),
            "SH" => ValidateSH(value, tag),
            "TM" => ValidateTM(value, tag),
            "UI" => ValidateUI(value, tag),
            "FL" or "FD" => ValidateFloat(value, tag),
            "SL" or "SS" => ValidateSignedInt(value, tag),
            "UL" or "US" => ValidateUnsignedInt(value, tag),
            _ => ValidationResult.Success() // Other VRs: basic validation passed
        };
    }

    /// <summary>
    /// Validates multi-valued strings (backslash-separated) by validating each component.
    /// </summary>
    private ValidationResult ValidateMultiValuedString(DicomTag tag, string vr, string value)
    {
        var components = value.Split('\\');
        var maxLength = GetMaxLength(vr);

        for (int i = 0; i < components.Length; i++)
        {
            var component = components[i];

            // Check length of each component
            if (maxLength > 0 && component.Length > maxLength)
            {
                return ValidationResult.Error(
                    $"Value component {i + 1} exceeds maximum length of {maxLength} characters for VR {vr}",
                    tag);
            }

            // Validate each component based on VR type
            var result = vr switch
            {
                "AE" => ValidateAE(component, tag),
                "AS" => ValidateAS(component, tag),
                "CS" => ValidateCS(component, tag),
                "DA" => ValidateDA(component, tag),
                "DS" => ValidateDS(component, tag),
                "DT" => ValidateDT(component, tag),
                "IS" => ValidateIS(component, tag),
                "LO" => ValidateLO(component, tag),
                "PN" => ValidatePN(component, tag),
                "SH" => ValidateSH(component, tag),
                "TM" => ValidateTM(component, tag),
                "UI" => ValidateUI(component, tag),
                _ => ValidationResult.Success()
            };

            if (!result.IsValid)
            {
                return ValidationResult.Error(
                    $"Value component {i + 1}: {result.ErrorMessage}",
                    tag);
            }
        }

        return ValidationResult.Success();
    }

    public IEnumerable<ValidationResult> ValidateDataset(DicomDataset dataset)
    {
        var results = new List<ValidationResult>();

        foreach (var item in dataset)
        {
            try
            {
                if (item is DicomElement element)
                {
                    var vr = element.ValueRepresentation.Code;
                    if (dataset.TryGetString(item.Tag, out var value) && !string.IsNullOrEmpty(value))
                    {
                        var result = ValidateValue(item.Tag, vr, value);
                        if (!result.IsValid)
                        {
                            results.Add(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating tag {Tag}", item.Tag);
            }
        }

        return results;
    }

    public int? GetMaxLength(string vr)
    {
        return VRMaxLengths.TryGetValue(vr, out var length) ? (length > 0 ? length : null) : null;
    }

    public bool IsValidForVR(string vr, string value)
    {
        return ValidateValue(DicomTag.Unknown, vr, value).IsValid;
    }

    // VR-specific validation methods

    private ValidationResult ValidateAE(string value, DicomTag tag)
    {
        // AE: uppercase, no leading/trailing spaces in value
        if (value.Trim() != value.TrimEnd())
        {
            return ValidationResult.Warning("AE values should not have trailing spaces", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateAS(string value, DicomTag tag)
    {
        // AS: 4 characters - nnnD/W/M/Y
        if (!ASRegex().IsMatch(value))
        {
            return ValidationResult.Error("Age String must be in format nnnD, nnnW, nnnM, or nnnY", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateCS(string value, DicomTag tag)
    {
        // CS: uppercase letters, digits, space, underscore
        // Allow empty components in multi-valued strings
        if (string.IsNullOrEmpty(value))
        {
            return ValidationResult.Success();
        }

        if (!CSRegex().IsMatch(value))
        {
            return ValidationResult.Error("Code String contains invalid characters (only uppercase letters, digits, space, underscore allowed)", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateDA(string value, DicomTag tag)
    {
        // DA: YYYYMMDD
        if (!DARegex().IsMatch(value))
        {
            return ValidationResult.Error("Date must be in YYYYMMDD format", tag);
        }

        // Validate it's a real date
        if (!DateTime.TryParseExact(value, "yyyyMMdd", null, 
            System.Globalization.DateTimeStyles.None, out _))
        {
            return ValidationResult.Error("Invalid date value", tag);
        }

        return ValidationResult.Success();
    }

    private ValidationResult ValidateDS(string value, DicomTag tag)
    {
        // DS: Decimal string
        if (!double.TryParse(value, out _))
        {
            return ValidationResult.Error("Decimal String must be a valid decimal number", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateDT(string value, DicomTag tag)
    {
        // DT: YYYYMMDDHHMMSS.FFFFFF&ZZXX
        if (value.Length < 4)
        {
            return ValidationResult.Error("DateTime must have at least 4 characters (YYYY)", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateIS(string value, DicomTag tag)
    {
        // IS: Integer string
        if (!long.TryParse(value, out _))
        {
            return ValidationResult.Error("Integer String must be a valid integer", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateLO(string value, DicomTag tag)
    {
        // LO: No control characters (except CR/LF)
        if (value.Any(c => char.IsControl(c) && c != '\r' && c != '\n'))
        {
            return ValidationResult.Error("Long String contains invalid control characters", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidatePN(string value, DicomTag tag)
    {
        // PN: Person Name - components separated by ^
        // family^given^middle^prefix^suffix
        var components = value.Split('^');
        if (components.Length > 5)
        {
            return ValidationResult.Warning("Person Name has more than 5 components", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateSH(string value, DicomTag tag)
    {
        // SH: No control characters
        if (value.Any(c => char.IsControl(c)))
        {
            return ValidationResult.Error("Short String contains invalid control characters", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateTM(string value, DicomTag tag)
    {
        // TM: HHMMSS.FFFFFF
        if (!TMRegex().IsMatch(value))
        {
            return ValidationResult.Error("Time must be in HHMMSS format", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateUI(string value, DicomTag tag)
    {
        // UI: digits and dots only, max 64 chars
        if (!UIRegex().IsMatch(value))
        {
            return ValidationResult.Error("UID must contain only digits and dots", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateFloat(string value, DicomTag tag)
    {
        if (!double.TryParse(value, out _))
        {
            return ValidationResult.Error("Value must be a valid floating-point number", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateSignedInt(string value, DicomTag tag)
    {
        if (!long.TryParse(value, out _))
        {
            return ValidationResult.Error("Value must be a valid signed integer", tag);
        }
        return ValidationResult.Success();
    }

    private ValidationResult ValidateUnsignedInt(string value, DicomTag tag)
    {
        if (!ulong.TryParse(value, out _))
        {
            return ValidationResult.Error("Value must be a valid unsigned integer", tag);
        }
        return ValidationResult.Success();
    }

    // Regex patterns using source generators for performance
    [GeneratedRegex(@"^\d{3}[DWMY]$")]
    private static partial Regex ASRegex();

    [GeneratedRegex(@"^[A-Z0-9_ ]*$")]
    private static partial Regex CSRegex();

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex DARegex();

    [GeneratedRegex(@"^\d{2,6}(\.\d{1,6})?$")]
    private static partial Regex TMRegex();

    [GeneratedRegex(@"^[\d.]+$")]
    private static partial Regex UIRegex();
}
