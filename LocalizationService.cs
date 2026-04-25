using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;

namespace MuaythaiApp;

public enum AppLanguage
{
    English,
    Polish
}

public static class LocalizationService
{
    private static readonly Dictionary<string, string> English = new()
    {
        ["Language"] = "Language",
        ["English"] = "English",
        ["Polish"] = "Polish",
        ["MainTitle"] = "MuaythaiApp",
        ["Definitions"] = "Definitions",
        ["Fighters"] = "Fighters",
        ["Clubs"] = "Clubs",
        ["Categories"] = "Categories",
        ["Matches"] = "Matches",
        ["FightResults"] = "Fight Results",
        ["MedalTable"] = "Medal Table",
        ["Reports"] = "Reports",
        ["ChangePasswords"] = "Change Passwords",
        ["DatabaseSync"] = "Database Sync",
        ["ChampionshipDefinitions"] = "Championship Definitions",
        ["ChampionshipSetup"] = "Championship Definitions",
        ["ChampionshipInformation"] = "Championship Information",
        ["ChampionshipRingInformation"] = "Championship Ring Information",
        ["ChampionshipCategoryInformation"] = "Championship Category Information",
        ["ChampionshipProcess"] = "Championship Process",
        ["RingInformation"] = "Ring Information",
        ["CategoryInformation"] = "Category Information",
        ["ClubInformation"] = "Club Information",
        ["AthleteInformation"] = "Athlete Information",
        ["AthleteControl"] = "Athlete Control",
        ["ChampionshipInformationEntry"] = "Championship Information Entry",
        ["ChampionshipRingInformationEntry"] = "Championship Ring Information Entry",
        ["ChampionshipCategoryEntry"] = "Championship Category Entry",
        ["AthleteScale"] = "Athlete Scale",
        ["GenderControl"] = "Gender Control",
        ["ScaleControl"] = "Scale Control",
        ["ApplyControls"] = "Apply Controls",
        ["AthleteScaleAndList"] = "Athlete List and Scale",
        ["DocumentControl"] = "Document Control",
        ["EligibilityStatus"] = "Eligibility Status",
        ["MedicalFitness"] = "Medical fitness",
        ["HighRiskInsurance"] = "NNW high-risk insurance",
        ["RegistrationForm"] = "Entry form original",
        ["GuardianConsent"] = "Parent or guardian consent",
        ["SeniorGuardianConsent"] = "Consent for Senior category",
        ["AmateurLicense2026"] = "Amateur license 2026",
        ["PolishCitizenship"] = "Polish citizenship",
        ["GenderVerified"] = "Gender confirmed",
        ["SelectAllDocuments"] = "Select all",
        ["ChampionshipName"] = "Championship Name",
        ["Address"] = "Address",
        ["StartDate"] = "Start Date",
        ["EndDate"] = "End Date",
        ["Rings"] = "Rings",
        ["ActiveCategoriesAndWeights"] = "Active Categories and Weights",
        ["SelectAllCategories"] = "Select All Categories",
        ["ClearSelection"] = "Clear Selection",
        ["CurrentDatabase"] = "Current database",
        ["SharedDatabasePath"] = "Shared database path",
        ["DatabaseSyncHint"] = "Use the same shared network database file path on both computers to see the same tournament data.",
        ["DatabaseSyncInfo"] = "If the target database file does not exist yet, the app copies your current data into it automatically.",
        ["DatabaseSyncSaved"] = "Database sync settings saved.",
        ["DatabaseSyncSaveFailed"] = "Database sync could not be saved",
        ["SharedDatabasePathRequired"] = "Enter a shared database path.",
        ["DatabasePathMustEndWithDb"] = "Database path must end with .db",
        ["InvalidDatabasePath"] = "Database path is not valid.",
        ["UsingLocalDatabase"] = "Now using this computer's local database.",
        ["UsingLocalDatabaseLabel"] = "Using local database",
        ["UsingSharedDatabaseLabel"] = "Using shared database",
        ["UseLocalDatabase"] = "Use Local Database",
        ["Save"] = "Save",
        ["Cancel"] = "Cancel",
        ["Logout"] = "Logout",
        ["LoggedInAsAdministrator"] = "Logged in as administrator",
        ["LoggedInAsUser"] = "Logged in as user",
        ["FullAccessEnabled"] = "Full access enabled. Administrator can edit fighters, clubs, matches and passwords.",
        ["LimitedAccessEnabled"] = "Limited access enabled. This account can only view categories, fight results, medal table and reports.",
        ["FighterRegistration"] = "Fighter Registration",
        ["FirstName"] = "First Name",
        ["LastName"] = "Last Name",
        ["Club"] = "Club",
        ["BirthYear"] = "Birth Year",
        ["Age"] = "Age",
        ["Weight"] = "Weight",
        ["Gender"] = "Gender",
        ["Category"] = "Category",
        ["WeightClass"] = "Weight Class",
        ["AddFighter"] = "Add Fighter",
        ["UpdateFighter"] = "Update Fighter",
        ["DeleteFighter"] = "Delete Fighter",
        ["ClearForm"] = "Clear Form",
        ["SearchFighter"] = "Search Fighter",
        ["SortBy"] = "Sort By",
        ["Name"] = "Name",
        ["Surname"] = "Surname",
        ["ClubsTitle"] = "Clubs",
        ["ClubName"] = "Club Name",
        ["Coach"] = "Coach",
        ["City"] = "City",
        ["Country"] = "Country",
        ["AddClub"] = "Add Club",
        ["UpdateClub"] = "Update Club",
        ["DeleteClub"] = "Delete Club",
        ["Clear"] = "Clear",
        ["Search"] = "Search",
        ["ExistingClubs"] = "Existing Clubs",
        ["CategoryManagement"] = "Category Management",
        ["Division"] = "Division",
        ["Info"] = "Info",
        ["All"] = "All",
        ["WeightRange"] = "Weight Range",
        ["Rounds"] = "Rounds",
        ["Break"] = "Break",
        ["WeightClasses"] = "Weight Classes",
        ["Day"] = "Day",
        ["Ring"] = "Ring",
        ["Judges"] = "Judges",
        ["AutoMatchMaker"] = "Auto Match Maker",
        ["Score"] = "Score",
        ["Bout"] = "Bout",
        ["Red"] = "Red",
        ["Blue"] = "Blue",
        ["Winner"] = "Winner",
        ["Method"] = "Method",
        ["Round"] = "Round",
        ["SearchWinner"] = "Search Winner",
        ["Scoreboard"] = "Scoreboard",
        ["BoutNo"] = "Bout No.",
        ["JudgesCount"] = "Judges Count",
        ["Judge"] = "Judge",
        ["RefereeName"] = "Referee Name",
        ["RedTotal"] = "Red Total",
        ["BlueTotal"] = "Blue Total",
        ["RedPointsWarnings"] = "Red points / warnings",
        ["BluePointsWarnings"] = "Blue points / warnings",
        ["RedPoints"] = "Red Points",
        ["BluePoints"] = "Blue Points",
        ["Total"] = "Total",
        ["Warning"] = "Warning",
        ["BetterStyle"] = "Better style",
        ["BetterDefense"] = "Better defense",
        ["Other"] = "Other",
        ["ResultMethod"] = "Result Method",
        ["ResultRound"] = "Result Round",
        ["SaveScore"] = "Save Score",
        ["NextFight"] = "Next Fight",
        ["ShowWinner"] = "Show Winner",
        ["ReportsTitle"] = "Reports",
        ["TournamentTitle"] = "Tournament Title",
        ["ReportType"] = "Report Type",
        ["Refresh"] = "Refresh",
        ["ExportCsv"] = "Export CSV",
        ["ExportPdf"] = "Export PDF",
        ["Print"] = "Print",
        ["Gold"] = "Gold",
        ["Silver"] = "Silver",
        ["Bronze"] = "Bronze",
        ["Medal"] = "Medal",
        ["Fighter"] = "Fighter",
        ["Date"] = "Date",
        ["DistributeRings"] = "Distribute Rings",
        ["DistributeMatchesToRings"] = "Distribute matches to rings",
        ["DistributeDayMatches"] = "Distribute Day {0} matches",
        ["RingDistributionHint"] = "Enter how many matches each ring should receive. Remaining matches go to the last ring with a count.",
        ["RingCountInvalid"] = "{0}: enter 0 or a positive number.",
        ["Apply"] = "Apply",
        ["CheckForUpdates"] = "Check for Updates",
        ["SaveActiveCategories"] = "Save Active Categories",
        ["Active"] = "Active",
        ["AgeCategory"] = "Age Category",
        ["AgeRange"] = "Age Range",
        ["Sort"] = "Sort",
        ["ClubDetails"] = "Club Details",
        ["CoachName"] = "Coach Name",
        ["ClubCity"] = "Club City",
        ["AthleteCount"] = "Athlete Count",
        ["Athletes"] = "Athletes",
        ["ClubAthleteList"] = "Club Athlete List",
        ["SelectClubToSeeAthleteDetails"] = "Select a club to see athlete details.",
        ["Athlete"] = "Athlete",
        ["Officials"] = "Officials",
        ["Timing"] = "Timing",
        ["Referee"] = "Referee",
        ["Password"] = "Password",
        ["Login"] = "Login",
        ["AccessType"] = "Access Type",
        ["MuaythaiAppLogin"] = "MuaythaiApp Login",
        ["LoginHint"] = "Choose access type and enter the password.",
        ["SavePasswords"] = "Save Passwords",
        ["CurrentAdministratorPassword"] = "Current Administrator Password",
        ["NewAdministratorPassword"] = "New Administrator Password",
        ["ConfirmAdministratorPassword"] = "Confirm Administrator Password",
        ["NewUserPassword"] = "New User Password",
        ["ConfirmUserPassword"] = "Confirm User Password",
        ["PasswordChangeHint"] = "Leave any new password empty if you do not want to change it.",
        ["ServerApiAddress"] = "Server API address",
        ["AutoUpdateGithubRepository"] = "Auto update GitHub repository",
        ["SaveUpdateSource"] = "Save Update Source",
        ["ClearUpdateSource"] = "Clear Update Source",
        ["UseRemoteApi"] = "Use Remote API",
        ["SaveDatabasePath"] = "Save Database Path",
        ["NameOrClub"] = "Name or club",
        ["ClubCoachCityOrCountry"] = "Club, coach, city or country",
        ["WinnerRedBlueOrCategory"] = "Winner, red, blue or category",
        ["Auto"] = "Auto"
    };

    private static readonly Dictionary<string, string> Polish = new()
    {
        ["Language"] = "Jezyk",
        ["English"] = "Angielski",
        ["Polish"] = "Polski",
        ["MainTitle"] = "MuaythaiApp",
        ["Definitions"] = "Definicje",
        ["Fighters"] = "Zawodnicy",
        ["Clubs"] = "Kluby",
        ["Categories"] = "Kategorie",
        ["Matches"] = "Walki",
        ["FightResults"] = "Wyniki Walk",
        ["MedalTable"] = "Tabela Medali",
        ["Reports"] = "Raporty",
        ["ChangePasswords"] = "Zmien Hasla",
        ["DatabaseSync"] = "Synchronizacja Bazy",
        ["ChampionshipDefinitions"] = "Definicje Mistrzostw",
        ["ChampionshipSetup"] = "Definicje Mistrzostw",
        ["ChampionshipInformation"] = "Informacje o Mistrzostwach",
        ["ChampionshipRingInformation"] = "Informacje o Ringach",
        ["ChampionshipCategoryInformation"] = "Informacje o Kategoriach",
        ["ChampionshipProcess"] = "Proces Mistrzostw",
        ["RingInformation"] = "Informacje o ringu",
        ["CategoryInformation"] = "Informacje o kategorii",
        ["ClubInformation"] = "Informacje o klubie",
        ["AthleteInformation"] = "Informacje o zawodniku",
        ["AthleteControl"] = "Kontrola zawodnika",
        ["ChampionshipInformationEntry"] = "Wpis Informacji o Mistrzostwach",
        ["ChampionshipRingInformationEntry"] = "Wpis Informacji o Ringach",
        ["ChampionshipCategoryEntry"] = "Wpis Kategorii Mistrzostw",
        ["AthleteScale"] = "Wazenie Zawodnikow",
        ["GenderControl"] = "Kontrola Plci",
        ["ScaleControl"] = "Kontrola Wagi",
        ["ApplyControls"] = "Zastosuj Kontrole",
        ["AthleteScaleAndList"] = "Lista zawodnikow i wazenie",
        ["DocumentControl"] = "Kontrola dokumentow",
        ["EligibilityStatus"] = "Status dopuszczenia",
        ["MedicalFitness"] = "Ksiazeczka lub zaswiadczenie sportowe",
        ["HighRiskInsurance"] = "Ubezpieczenie NNW wysokiego ryzyka",
        ["RegistrationForm"] = "Oryginal formularza zgloszeniowego",
        ["GuardianConsent"] = "Zgoda rodzica lub opiekuna",
        ["SeniorGuardianConsent"] = "Zgoda na start w kategorii Senior",
        ["AmateurLicense2026"] = "Licencja amatorska 2026",
        ["PolishCitizenship"] = "Obywatelstwo polskie",
        ["GenderVerified"] = "Kontrola plci potwierdzona",
        ["SelectAllDocuments"] = "Zaznacz wszystko",
        ["ChampionshipName"] = "Nazwa Mistrzostw",
        ["Address"] = "Adres",
        ["StartDate"] = "Data Rozpoczecia",
        ["EndDate"] = "Data Zakonczenia",
        ["Rings"] = "Ringi",
        ["ActiveCategoriesAndWeights"] = "Aktywne Kategorie i Wagi",
        ["SelectAllCategories"] = "Zaznacz Wszystkie Kategorie",
        ["ClearSelection"] = "Wyczysc Zaznaczenie",
        ["CurrentDatabase"] = "Aktualna baza",
        ["SharedDatabasePath"] = "Sciezka wspolnej bazy",
        ["DatabaseSyncHint"] = "Uzyj tej samej sciezki do wspolnego pliku bazy sieciowej na obu komputerach, aby widziec te same dane turnieju.",
        ["DatabaseSyncInfo"] = "Jesli docelowy plik bazy jeszcze nie istnieje, aplikacja automatycznie skopiuje do niego biezace dane.",
        ["DatabaseSyncSaved"] = "Ustawienia synchronizacji bazy zapisane.",
        ["DatabaseSyncSaveFailed"] = "Nie udalo sie zapisac ustawien synchronizacji bazy",
        ["SharedDatabasePathRequired"] = "Wpisz sciezke wspolnej bazy.",
        ["DatabasePathMustEndWithDb"] = "Sciezka bazy musi konczyc sie na .db",
        ["InvalidDatabasePath"] = "Sciezka bazy jest nieprawidlowa.",
        ["UsingLocalDatabase"] = "Aplikacja uzywa teraz lokalnej bazy tego komputera.",
        ["UsingLocalDatabaseLabel"] = "Uzywana lokalna baza",
        ["UsingSharedDatabaseLabel"] = "Uzywana wspolna baza",
        ["UseLocalDatabase"] = "Uzyj Lokalnej Bazy",
        ["Save"] = "Zapisz",
        ["Cancel"] = "Anuluj",
        ["Logout"] = "Wyloguj",
        ["LoggedInAsAdministrator"] = "Zalogowano jako administrator",
        ["LoggedInAsUser"] = "Zalogowano jako uzytkownik",
        ["FullAccessEnabled"] = "Pelny dostep wlaczony. Administrator moze edytowac zawodnikow, kluby, walki i hasla.",
        ["LimitedAccessEnabled"] = "Ograniczony dostep wlaczony. To konto moze tylko przegladac kategorie, wyniki walk, tabele medali i raporty.",
        ["FighterRegistration"] = "Rejestracja Zawodnikow",
        ["FirstName"] = "Imie",
        ["LastName"] = "Nazwisko",
        ["Club"] = "Klub",
        ["BirthYear"] = "Rok Urodzenia",
        ["Age"] = "Wiek",
        ["Weight"] = "Waga",
        ["Gender"] = "Plec",
        ["Category"] = "Kategoria",
        ["WeightClass"] = "Kategoria Wagowa",
        ["AddFighter"] = "Dodaj Zawodnika",
        ["UpdateFighter"] = "Aktualizuj Zawodnika",
        ["DeleteFighter"] = "Usun Zawodnika",
        ["ClearForm"] = "Wyczysc Formularz",
        ["SearchFighter"] = "Szukaj Zawodnika",
        ["SortBy"] = "Sortuj Wedlug",
        ["Name"] = "Imie",
        ["Surname"] = "Nazwisko",
        ["ClubsTitle"] = "Kluby",
        ["ClubName"] = "Nazwa Klubu",
        ["Coach"] = "Trener",
        ["City"] = "Miasto",
        ["Country"] = "Kraj",
        ["AddClub"] = "Dodaj Klub",
        ["UpdateClub"] = "Aktualizuj Klub",
        ["DeleteClub"] = "Usun Klub",
        ["Clear"] = "Wyczysc",
        ["Search"] = "Szukaj",
        ["ExistingClubs"] = "Istniejace Kluby",
        ["CategoryManagement"] = "Zarzadzanie Kategoriami",
        ["Division"] = "Dywizja",
        ["Info"] = "Informacja",
        ["All"] = "Wszystko",
        ["WeightRange"] = "Zakres Wagi",
        ["Rounds"] = "Rundy",
        ["Break"] = "Przerwa",
        ["WeightClasses"] = "Klasy Wagowe",
        ["Day"] = "Dzien",
        ["Ring"] = "Ring",
        ["Judges"] = "Sedziowie",
        ["AutoMatchMaker"] = "Automatyczne Losowanie",
        ["Score"] = "Punktacja",
        ["Bout"] = "Pojedynek",
        ["Red"] = "Czerwony",
        ["Blue"] = "Niebieski",
        ["Winner"] = "Zwyciezca",
        ["Method"] = "Metoda",
        ["Round"] = "Runda",
        ["SearchWinner"] = "Szukaj Zwyciezcy",
        ["Scoreboard"] = "Tablica Wynikow",
        ["BoutNo"] = "Nr Walki",
        ["JudgesCount"] = "Liczba Sedziow",
        ["Judge"] = "Sedzia",
        ["RefereeName"] = "Sedzia Glowny",
        ["RedTotal"] = "Suma Czerwony",
        ["BlueTotal"] = "Suma Niebieski",
        ["RedPointsWarnings"] = "Punkty / ostrzezenia czerwony",
        ["BluePointsWarnings"] = "Punkty / ostrzezenia niebieski",
        ["RedPoints"] = "Punkty Czerwony",
        ["BluePoints"] = "Punkty Niebieski",
        ["Total"] = "Suma",
        ["Warning"] = "Ostrzezenie",
        ["BetterStyle"] = "Lepszy styl",
        ["BetterDefense"] = "Lepsza obrona",
        ["Other"] = "Inne",
        ["ResultMethod"] = "Metoda Wyniku",
        ["ResultRound"] = "Runda Wyniku",
        ["SaveScore"] = "Zapisz Punkty",
        ["NextFight"] = "Nastepna Walka",
        ["ShowWinner"] = "Pokaz Zwyciezce",
        ["ReportsTitle"] = "Raporty",
        ["TournamentTitle"] = "Nazwa Turnieju",
        ["ReportType"] = "Typ Raportu",
        ["Refresh"] = "Odswiez",
        ["ExportCsv"] = "Eksport CSV",
        ["ExportPdf"] = "Eksport PDF",
        ["Print"] = "Drukuj",
        ["Gold"] = "Zloto",
        ["Silver"] = "Srebro",
        ["Bronze"] = "Braz",
        ["Medal"] = "Medal",
        ["Fighter"] = "Zawodnik",
        ["Date"] = "Data",
        ["DistributeRings"] = "Rozdziel ringi",
        ["DistributeMatchesToRings"] = "Rozdziel walki na ringi",
        ["DistributeDayMatches"] = "Rozdziel walki dnia {0}",
        ["RingDistributionHint"] = "Wpisz, ile walk ma otrzymac kazdy ring. Pozostale walki trafia do ostatniego ringu z podana liczba.",
        ["RingCountInvalid"] = "{0}: wpisz 0 lub liczbe dodatnia.",
        ["Apply"] = "Zastosuj",
        ["CheckForUpdates"] = "Sprawdz aktualizacje",
        ["SaveActiveCategories"] = "Zapisz aktywne kategorie",
        ["Active"] = "Aktywne",
        ["AgeCategory"] = "Kategoria wiekowa",
        ["AgeRange"] = "Zakres wieku",
        ["Sort"] = "Sortowanie",
        ["ClubDetails"] = "Szczegoly klubu",
        ["CoachName"] = "Imie trenera",
        ["ClubCity"] = "Miasto klubu",
        ["AthleteCount"] = "Liczba zawodnikow",
        ["Athletes"] = "Zawodnicy",
        ["ClubAthleteList"] = "Lista zawodnikow klubu",
        ["SelectClubToSeeAthleteDetails"] = "Wybierz klub, aby zobaczyc szczegoly zawodnikow.",
        ["Athlete"] = "Zawodnik",
        ["Officials"] = "Sedziowie",
        ["Timing"] = "Czas",
        ["Referee"] = "Sedzia glowny",
        ["Password"] = "Haslo",
        ["Login"] = "Zaloguj",
        ["AccessType"] = "Typ dostepu",
        ["MuaythaiAppLogin"] = "Logowanie MuaythaiApp",
        ["LoginHint"] = "Wybierz typ dostepu i wpisz haslo.",
        ["SavePasswords"] = "Zapisz hasla",
        ["CurrentAdministratorPassword"] = "Aktualne haslo administratora",
        ["NewAdministratorPassword"] = "Nowe haslo administratora",
        ["ConfirmAdministratorPassword"] = "Potwierdz haslo administratora",
        ["NewUserPassword"] = "Nowe haslo uzytkownika",
        ["ConfirmUserPassword"] = "Potwierdz haslo uzytkownika",
        ["PasswordChangeHint"] = "Pozostaw nowe haslo puste, jesli nie chcesz go zmieniac.",
        ["ServerApiAddress"] = "Adres API serwera",
        ["AutoUpdateGithubRepository"] = "Repozytorium GitHub aktualizacji",
        ["SaveUpdateSource"] = "Zapisz zrodlo aktualizacji",
        ["ClearUpdateSource"] = "Wyczysc zrodlo aktualizacji",
        ["UseRemoteApi"] = "Uzyj zdalnego API",
        ["SaveDatabasePath"] = "Zapisz sciezke bazy",
        ["NameOrClub"] = "Imie, nazwisko lub klub",
        ["ClubCoachCityOrCountry"] = "Klub, trener, miasto lub kraj",
        ["WinnerRedBlueOrCategory"] = "Zwyciezca, czerwony, niebieski lub kategoria",
        ["Auto"] = "Automatycznie"
    };

    private static readonly Dictionary<string, string> EnglishValueToKey = English
        .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Key, x => x.First().Key, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> PolishValueToKey = Polish
        .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Key, x => x.First().Key, StringComparer.OrdinalIgnoreCase);

    public static event Action? LanguageChanged;

    public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.English;

    public static void SetLanguage(AppLanguage language)
    {
        if (CurrentLanguage == language)
            return;

        CurrentLanguage = language;
        LanguageChanged?.Invoke();
    }

    public static string T(string key)
    {
        var source = CurrentLanguage == AppLanguage.Polish ? Polish : English;
        return source.TryGetValue(key, out var value) ? value : key;
    }

    public static string Format(string key, params object[] args)
        => string.Format(T(key), args);

    public static string TranslateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (EnglishValueToKey.TryGetValue(text, out var key) ||
            PolishValueToKey.TryGetValue(text, out key))
            return T(key);

        if ((text.StartsWith("Day ", StringComparison.OrdinalIgnoreCase) ||
             text.StartsWith("Dzien ", StringComparison.OrdinalIgnoreCase)) &&
            int.TryParse(text[(text.IndexOf(' ') + 1)..], out var dayNumber))
            return $"{T("Day")} {dayNumber}";

        if ((text.StartsWith("Round ", StringComparison.OrdinalIgnoreCase) ||
             text.StartsWith("Runda ", StringComparison.OrdinalIgnoreCase)) &&
            int.TryParse(text[(text.IndexOf(' ') + 1)..], out var roundNumber))
            return $"{T("Round")} {roundNumber}";

        if ((text.StartsWith("Judge ", StringComparison.OrdinalIgnoreCase) ||
             text.StartsWith("Sedzia ", StringComparison.OrdinalIgnoreCase)) &&
            int.TryParse(text[(text.IndexOf(' ') + 1)..], out var judgeNumber))
            return $"{T("Judge")} {judgeNumber}";

        return text;
    }

    public static void LocalizeControlTree(Control root)
    {
        if (root is Window window && !string.IsNullOrWhiteSpace(window.Title))
            window.Title = TranslateText(window.Title);

        if (root is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
            textBlock.Text = TranslateText(textBlock.Text);

        if (root is ContentControl contentControl && contentControl.Content is string content)
            contentControl.Content = TranslateText(content);

        if (root is HeaderedContentControl headeredContentControl && headeredContentControl.Header is string header)
            headeredContentControl.Header = TranslateText(header);

        if (root is TextBox textBox && !string.IsNullOrWhiteSpace(textBox.Watermark))
            textBox.Watermark = TranslateText(textBox.Watermark);

        foreach (var child in root.GetLogicalChildren().OfType<Control>())
            LocalizeControlTree(child);
    }
}
