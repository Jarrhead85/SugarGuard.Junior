// SugarGuard.Web/ViewModels/DoctorNoteVm.cs
namespace SugarGuard.Web.ViewModels;

/// <summary>UI-модель заметки врача.</summary>
public sealed class DoctorNoteVm
{
    public Guid      NoteId        { get; set; }
    public Guid      DoctorUserId  { get; set; }
    public string    DoctorName    { get; set; } = string.Empty;
    public Guid      ChildId       { get; set; }
    public Guid?     MeasurementId { get; set; }
    public string    NoteText      { get; set; } = string.Empty;
    public bool      IsImportant   { get; set; }
    public DateTime  CreatedAt     { get; set; }
    public DateTime? UpdatedAt     { get; set; }
}
