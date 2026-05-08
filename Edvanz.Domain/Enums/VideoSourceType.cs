namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies the external provider that hosts a video referenced by a
/// <c>VideoAsset</c>. Stored as a <c>tinyint</c> column to keep the row narrow.
///
/// REQ-VCM-FR-01: Only YouTube and Google Drive sources are supported. Adding a new
/// provider requires (1) a new enum value, (2) URL-pattern support in
/// <c>IVideoUrlParser</c>, and (3) an embed-URL builder branch in the service layer.
///
/// Numeric values are part of the persisted contract — never reorder or repurpose.
/// </summary>
public enum VideoSourceType : byte
{
    /// <summary>YouTube watch / shorts / youtu.be / embed URLs.</summary>
    YouTube = 1,

    /// <summary>Google Drive file-share URLs.</summary>
    GoogleDrive = 2
}
