using SugarGuard.Web.Services;

namespace SugarGuard.Web.ViewModels;

public sealed class DoctorNoteListItemVm
{
    public DoctorNoteListItemVm(DoctorPatientSummaryVm patient, DoctorNoteVm note)
    {
        Patient = patient;
        Note = note;
    }

    public DoctorPatientSummaryVm Patient { get; }

    public DoctorNoteVm Note { get; }
}
