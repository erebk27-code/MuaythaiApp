using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MuaythaiApp;

public class ChampionshipProcessEntry : INotifyPropertyChanged
{
    private int fighterId;
    private int dayNumber;
    private string fighterName = "";
    private string clubName = "";
    private int age;
    private string gender = "";
    private string ageCategory = "";
    private string weightCategory = "";
    private string allowedWeightText = "";
    private double? allowedWeightMax;
    private bool isOpenWeight;
    private string measuredWeightText = "";
    private bool genderConfirmed = true;
    private bool licensePresented;
    private bool medicalReportPresented;
    private bool identityPresented;
    private bool insurancePresented;
    private bool registrationFormPresented;
    private bool guardianConsentPresented;
    private bool seniorGuardianConsentPresented;
    private bool amateurLicense2026Presented;
    private bool polishCitizenshipConfirmed;
    private string scaleStatus = "Pending";
    private string controlNote = "";
    private bool isDisqualified;
    private bool requiresPolishCitizenship;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int FighterId
    {
        get => fighterId;
        set => SetProperty(ref fighterId, value);
    }

    public int DayNumber
    {
        get => dayNumber;
        set => SetProperty(ref dayNumber, value);
    }

    public string FighterName
    {
        get => fighterName;
        set => SetProperty(ref fighterName, value);
    }

    public string ClubName
    {
        get => clubName;
        set => SetProperty(ref clubName, value);
    }

    public int Age
    {
        get => age;
        set
        {
            if (!SetProperty(ref age, value))
                return;

            OnPropertyChanged(nameof(IsMinor));
            OnPropertyChanged(nameof(RequiresGuardianConsent));
            OnPropertyChanged(nameof(RequiresSeniorGuardianConsent));
            OnPropertyChanged(nameof(RequirementHint));
        }
    }

    public string Gender
    {
        get => gender;
        set => SetProperty(ref gender, value);
    }

    public string AgeCategory
    {
        get => ageCategory;
        set
        {
            if (!SetProperty(ref ageCategory, value))
                return;

            OnPropertyChanged(nameof(CategoryDisplay));
            OnPropertyChanged(nameof(IsSeniorCategory));
            OnPropertyChanged(nameof(RequiresSeniorGuardianConsent));
            OnPropertyChanged(nameof(RequirementHint));
        }
    }

    public string WeightCategory
    {
        get => weightCategory;
        set
        {
            if (SetProperty(ref weightCategory, value))
                OnPropertyChanged(nameof(CategoryDisplay));
        }
    }

    public string AllowedWeightText
    {
        get => allowedWeightText;
        set => SetProperty(ref allowedWeightText, value);
    }

    public double? AllowedWeightMax
    {
        get => allowedWeightMax;
        set => SetProperty(ref allowedWeightMax, value);
    }

    public bool IsOpenWeight
    {
        get => isOpenWeight;
        set => SetProperty(ref isOpenWeight, value);
    }

    public string MeasuredWeightText
    {
        get => measuredWeightText;
        set => SetProperty(ref measuredWeightText, value);
    }

    public bool GenderConfirmed
    {
        get => genderConfirmed;
        set => SetProperty(ref genderConfirmed, value);
    }

    public bool LicensePresented
    {
        get => licensePresented;
        set => SetProperty(ref licensePresented, value);
    }

    public bool MedicalReportPresented
    {
        get => medicalReportPresented;
        set => SetProperty(ref medicalReportPresented, value);
    }

    public bool IdentityPresented
    {
        get => identityPresented;
        set => SetProperty(ref identityPresented, value);
    }

    public bool InsurancePresented
    {
        get => insurancePresented;
        set => SetProperty(ref insurancePresented, value);
    }

    public bool RegistrationFormPresented
    {
        get => registrationFormPresented;
        set => SetProperty(ref registrationFormPresented, value);
    }

    public bool GuardianConsentPresented
    {
        get => guardianConsentPresented;
        set => SetProperty(ref guardianConsentPresented, value);
    }

    public bool SeniorGuardianConsentPresented
    {
        get => seniorGuardianConsentPresented;
        set => SetProperty(ref seniorGuardianConsentPresented, value);
    }

    public bool AmateurLicense2026Presented
    {
        get => amateurLicense2026Presented;
        set => SetProperty(ref amateurLicense2026Presented, value);
    }

    public bool PolishCitizenshipConfirmed
    {
        get => polishCitizenshipConfirmed;
        set => SetProperty(ref polishCitizenshipConfirmed, value);
    }

    public string ScaleStatus
    {
        get => scaleStatus;
        set => SetProperty(ref scaleStatus, value);
    }

    public string ControlNote
    {
        get => controlNote;
        set => SetProperty(ref controlNote, value);
    }

    public bool IsDisqualified
    {
        get => isDisqualified;
        set => SetProperty(ref isDisqualified, value);
    }

    public string CategoryDisplay => $"{AgeCategory} / {WeightCategory}".Trim(' ', '/');
    public bool IsMinor => Age > 0 && Age < 18;
    public bool IsSeniorCategory => !string.IsNullOrWhiteSpace(AgeCategory) &&
                                    AgeCategory.Contains("senior", System.StringComparison.OrdinalIgnoreCase);
    public bool RequiresGuardianConsent => IsMinor;
    public bool RequiresSeniorGuardianConsent => IsMinor && IsSeniorCategory;
    public bool RequiresPolishCitizenship
    {
        get => requiresPolishCitizenship;
        set
        {
            if (!SetProperty(ref requiresPolishCitizenship, value))
                return;

            OnPropertyChanged(nameof(RequirementHint));
        }
    }

    public string RequirementHint
    {
        get
        {
            var hints = new List<string>();

            if (RequiresGuardianConsent)
                hints.Add(LocalizationService.CurrentLanguage == AppLanguage.Polish
                    ? "wymagana zgoda rodzica/opiekuna"
                    : "guardian consent required");

            if (RequiresSeniorGuardianConsent)
                hints.Add(LocalizationService.CurrentLanguage == AppLanguage.Polish
                    ? "wymagana zgoda na start w Senior"
                    : "senior-category consent required");

            if (RequiresPolishCitizenship)
                hints.Add(LocalizationService.CurrentLanguage == AppLanguage.Polish
                    ? "wymagane obywatelstwo polskie"
                    : "Polish citizenship required");

            return hints.Count == 0
                ? (LocalizationService.CurrentLanguage == AppLanguage.Polish ? "standardowa kontrola" : "standard control")
                : string.Join(" | ", hints);
        }
    }

    public string MedicalFitnessLabel => LocalizationService.T("MedicalFitness");
    public string InsuranceLabel => LocalizationService.T("HighRiskInsurance");
    public string RegistrationFormLabel => LocalizationService.T("RegistrationForm");
    public string GuardianConsentLabel => LocalizationService.T("GuardianConsent");
    public string SeniorGuardianConsentLabel => LocalizationService.T("SeniorGuardianConsent");
    public string AmateurLicense2026Label => LocalizationService.T("AmateurLicense2026");
    public string PolishCitizenshipLabel => LocalizationService.T("PolishCitizenship");
    public string GenderVerifiedLabel => LocalizationService.T("GenderVerified");
    public string SelectAllDocumentsLabel => LocalizationService.T("SelectAllDocuments");

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (!string.IsNullOrWhiteSpace(propertyName))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
