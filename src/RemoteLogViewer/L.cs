using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Markup;

namespace RemoteLogViewer;

/// Localization: the Italian text is the key, English comes from `En`. A missing entry falls back to the Italian text.
/// Language comes from `lang.txt` next to sessions.json, else the OS UI language; `SetLang` switches it live (XAML bindings refresh, `Changed` fires).
public static class L
{
    public static readonly string[] Languages = ["it", "en"];

    /// Tests rely on the Italian messages, so "it" is the default until `Init` runs.
    public static string Lang { get; set; } = "it";

    public static string LangFile => Path.Combine(Path.GetDirectoryName(SessionStore.DefaultPath)!, "lang.txt");

    public static string GuideResource => $"RemoteLogViewer.Guida.{Lang}.html";
    public static string GuideFile => $"guide.{Lang}.html";

    public static void Init()
    {
        string? saved = null;
        try { if (File.Exists(LangFile)) saved = File.ReadAllText(LangFile).Trim(); } catch (IOException) { }
        Apply(Languages.Contains(saved) ? saved! : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it" ? "it" : "en");
    }

    /// Raised on the UI thread after a language switch: refresh text that was set from code.
    public static event Action? Changed;

    public static void SetLang(string lang)
    {
        if (lang == Lang) return;
        Apply(lang);
        Changed?.Invoke();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LangFile)!);
            File.WriteAllText(LangFile, lang);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // not remembered, still switched
    }

    private static void Apply(string lang)
    {
        Lang = lang;
        var culture = CultureInfo.GetCultureInfo(lang);
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        LocSource.Instance.Bump();
    }

    /// Binding that follows the language, for text set from code on a DependencyProperty.
    public static BindingBase Bind(string it) => new TExtension(it).AsBinding();

    public static string T(string it) => Translate(it, Lang);

    public static string Translate(string it, string lang) => lang == "en" && En.TryGetValue(it, out var en) ? en : it;

    public static string F(string it, params object[] args) => string.Format(T(it), args);

    private static readonly Dictionary<string, string> En = new()
    {
        // ---- main window ----
        ["Sessioni salvate"] = "Saved sessions",
        ["Avvia"] = "Start",
        ["Nuova"] = "New",
        ["Modifica"] = "Edit",
        ["Duplica"] = "Duplicate",
        ["Elimina"] = "Delete",
        ["Collezione"] = "Collection",
        ["Nuova sessione nella collezione selezionata"] = "New session in the selected collection",
        ["Modifica sessione o rinomina collezione (F2)"] = "Edit session or rename collection (F2)",
        ["Nuova collezione dentro quella selezionata. Tasto destro per altre azioni (sposta, esporta, importa)"] = "New collection inside the selected one. Right-click for more actions (move, export, import)",
        ["Credenziali…"] = "Credentials…",
        ["Utente/password condivisi tra più sessioni: cambi la password in un solo punto"] = "User/password shared by several sessions: change the password in one place",
        ["Affianca"] = "Side by side",
        ["Mostra tutte le sessioni aperte contemporaneamente"] = "Show all open sessions at once",
        ["Guida"] = "Help",
        ["Guida utente (F1)"] = "User guide (F1)",
        ["Lingua"] = "Language",
        ["Lingua dell'interfaccia e della guida"] = "Language of the interface and the guide",
        ["Doppio click su una sessione salvata per avviarla, oppure crea una Nuova sessione."] = "Double-click a saved session to start it, or create a New session.",
        ["Collezioni Remote Log Viewer (*.rlv.json)|*.rlv.json|JSON (*.json)|*.json"] = "Remote Log Viewer collections (*.rlv.json)|*.rlv.json|JSON (*.json)|*.json",
        [" (copia)"] = " (copy)",
        ["Eliminare la sessione \"{0}\"?"] = "Delete session \"{0}\"?",
        ["Eliminare la collezione \"{0}\"?"] = "Delete collection \"{0}\"?",
        ["Eliminare la collezione \"{0}\" con {1} sessioni e {2} sottocollezioni?"] = "Delete collection \"{0}\" with {1} sessions and {2} subcollections?",
        ["Nuova collezione"] = "New collection",
        ["Rinomina collezione"] = "Rename collection",
        ["_Nome"] = "_Name",
        ["N_ome"] = "Na_me",
        ["Esiste già una collezione con questo nome."] = "A collection with this name already exists.",
        ["Nuova sessione qui"] = "New session here",
        ["Nuova sottocollezione"] = "New subcollection",
        ["Rinomina"] = "Rename",
        ["Esporta…"] = "Export…",
        ["Importa qui…"] = "Import here…",
        ["Nuova sessione"] = "New session",
        ["Esporta tutto…"] = "Export all…",
        ["Importa…"] = "Import…",
        ["Sposta in…"] = "Move to…",
        ["(radice)"] = "(root)",
        ["Esporta collezione"] = "Export collection",
        ["Importa collezione"] = "Import collection",
        ["Esportazione non riuscita: {0}"] = "Export failed: {0}",
        ["Importazione non riuscita: {0}"] = "Import failed: {0}",
        ["Importate {0} sessioni. Le password non sono incluse: inseriscile con Modifica."] = "Imported {0} sessions. Passwords are not included: enter them with Edit.",
        ["Errori arrivati mentre la scheda non era visibile"] = "Errors that arrived while the tab was not visible",
        ["{0} ({1} errori)"] = "{0} ({1} errors)",
        ["Impossibile aprire la guida ({0}).\nFile: {1}"] = "Cannot open the guide ({0}).\nFile: {1}",

        // ---- log view ----
        ["Stop"] = "Stop",
        ["Scorrimento automatico"] = "Automatic scrolling",
        ["A capo"] = "Wrap",
        ["Manda a capo le righe lunghe"] = "Wrap long lines",
        ["Pulisci"] = "Clear",
        ["▲ Precedenti"] = "▲ Older",
        ["Carica righe precedenti a quelle lette"] = "Load lines before the ones already read",
        ["Salva…"] = "Save…",
        ["Salva le righe visibili in un file (Ctrl+S)"] = "Save the visible lines to a file (Ctrl+S)",
        ["Filtro:"] = "Filter:",
        ["Mostra solo le righe che contengono il testo"] = "Show only lines containing the text",
        ["Contesto:"] = "Context:",
        ["Righe mostrate prima e dopo ogni riga filtrata (0–50)"] = "Lines shown before and after each filtered line (0–50)",
        ["Mostra righe ERROR"] = "Show ERROR lines",
        ["Mostra righe WARN"] = "Show WARN lines",
        ["Mostra righe INFO"] = "Show INFO lines",
        ["Mostra righe DEBUG/TRACE"] = "Show DEBUG/TRACE lines",
        ["Cerca:"] = "Search:",
        ["Precedente (Shift+F3)"] = "Previous (Shift+F3)",
        ["Successiva (F3)"] = "Next (F3)",
        [" · {0} trovate"] = " · {0} found",
        ["{0} righe{1}"] = "{0} lines{1}",
        ["{0}/{1} righe{2}"] = "{0}/{1} lines{2}",
        ["Buffer pieno ({0:N0} righe): usa Pulisci per fare spazio."] = "Buffer full ({0:N0} lines): use Clear to make room.",
        ["Verranno caricate solo {0:N0} righe: il buffer è limitato a {1:N0}."] = "Only {0:N0} lines will be loaded: the buffer is limited to {1:N0}.",
        ["Log (*.log)|*.log|Tutti i file (*.*)|*.*"] = "Log (*.log)|*.log|All files (*.*)|*.*",
        ["Salva righe visibili"] = "Save visible lines",
        ["Salvataggio non riuscito: {0}"] = "Save failed: {0}",

        // ---- session edit / credentials / dialogs ----
        ["Sessione"] = "Session",
        ["Credenziali"] = "Credentials",
        ["_Share"] = "_Share",
        ["\\\\server\\share, es. \\\\nts11050\\E$"] = "\\\\server\\share, e.g. \\\\nts11050\\E$",
        ["_Credenziale"] = "_Credential",
        ["Credenziale salvata (gestiscile con 'Credenziali…'), oppure utente/password solo per questa sessione"] = "Saved credential (manage them with 'Credentials…'), or user/password for this session only",
        ["(utente/password qui sotto)"] = "(user/password below)",
        ["_Utente"] = "_User",
        ["DOMINIO\\utente. Vuoto = utente Windows corrente"] = "DOMAIN\\user. Empty = current Windows user",
        ["DOMINIO\\utente"] = "DOMAIN\\user",
        ["_Password"] = "_Password",
        ["Vuota in modifica = invariata"] = "Empty when editing = unchanged",
        ["_File"] = "_File",
        ["Relativo alla share (logs\\app.log) o UNC completo. Usa {date:yyyy_MM_dd} per una data variabile, scelta all'avvio."] = "Relative to the share (logs\\app.log) or a full UNC path. Use {date:yyyy_MM_dd} for a variable date, chosen at start.",
        ["Il file di _oggi non ha la data"] = "Today's file has _no date",
        ["Solo il file corrente è senza data (es. oggi app.log, ieri app.2026-09-29.log). Metti il separatore nel formato tra apici: app{date:'.'yyyy-MM-dd}.log"] = "Only the current file has no date (e.g. today app.log, yesterday app.2026-09-29.log). Put the separator inside the format, quoted: app{date:'.'yyyy-MM-dd}.log",
        ["_Righe iniziali"] = "Initial _lines",
        ["_Encoding"] = "_Encoding",
        ["_Test connessione"] = "_Test connection",
        ["Salva"] = "Save",
        ["Annulla"] = "Cancel",
        ["Chiudi"] = "Close",
        ["_Nuova"] = "_New",
        ["_Elimina"] = "_Delete",
        ["_Salva"] = "_Save",
        ["Credenziali salvate"] = "Saved credentials",
        ["Password salvata non leggibile su questo PC/utente: reinseriscila."] = "Saved password cannot be read on this PC/user: enter it again.",
        ["Anteprima (oggi): {0}"] = "Preview (today): {0}",
        ["Anteprima (ieri): {0}"] = "Preview (yesterday): {0}",
        ["Righe iniziali non valide."] = "Invalid initial lines.",
        ["Connessione…"] = "Connecting…",
        ["OK: file trovato ({0:N0} KB)."] = "OK: file found ({0:N0} KB).",
        ["Share OK, ma il file non esiste (la sessione resterà in attesa)."] = "Share OK, but the file does not exist (the session will wait for it).",
        ["Nuova credenziale."] = "New credential.",
        ["Usata da {0} sessioni."] = "Used by {0} sessions.",
        ["Utente obbligatorio."] = "User required.",
        ["Esiste già una credenziale con questo nome."] = "A credential with this name already exists.",
        ["Salvata. Usata da {0} sessioni (le sessioni già aperte la useranno alla prossima apertura)."] = "Saved. Used by {0} sessions (sessions already open will use it the next time they are opened).",
        ["Eliminare la credenziale \"{0}\"?"] = "Delete credential \"{0}\"?",
        ["La credenziale \"{0}\" è usata da {1} sessioni, che useranno utente/password propri (se vuoti, l'utente Windows corrente). Eliminarla?"] = "Credential \"{0}\" is used by {1} sessions, which will use their own user/password (if empty, the current Windows user). Delete it?",
        ["_Data del log"] = "Log _date",
        ["Avvia \"{0}\""] = "Start \"{0}\"",
        ["Verifica dei file disponibili…"] = "Checking available files…",
        ["Impossibile verificare i file: {0}"] = "Cannot check files: {0}",
        ["I giorni barrati non hanno un file di log."] = "Crossed-out days have no log file.",
        ["Scegli una data."] = "Pick a date.",
        [" (file presente)"] = " (file present)",
        [" (file non trovato: la sessione resterà in attesa)"] = " (file not found: the session will wait for it)",
        ["Anteprima: {0}"] = "Preview: {0}",
        ["Nome obbligatorio."] = "Name required.",

        // ---- session status / markers ----
        ["Ferma"] = "Stopped",
        ["Attiva"] = "Active",
        ["Riconnessione…"] = "Reconnecting…",
        ["In attesa del file…"] = "Waiting for the file…",
        ["— nuovo giorno: {0:yyyy-MM-dd} —"] = "— new day: {0:yyyy-MM-dd} —",
        ["— riconnesso —"] = "— reconnected —",
        ["— file troncato o ruotato: lettura dall'inizio —"] = "— file truncated or rotated: reading from the start —",
        ["— file scomparso, in attesa —"] = "— file gone, waiting —",
        ["— disconnesso: {0} —"] = "— disconnected: {0} —",
        ["Disconnesso, nuovo tentativo tra 5 s ({0})"] = "Disconnected, retrying in 5 s ({0})",

        // ---- model messages ----
        ["Accesso negato."] = "Access denied.",
        ["Server o share non raggiungibile."] = "Server or share unreachable.",
        ["Credenziali non valide."] = "Invalid credentials.",
        ["Esiste già una connessione a questo server con credenziali diverse. Chiudila (net use \\\\server\\share /delete) o usa le stesse credenziali."] = "A connection to this server with different credentials already exists. Close it (net use \\\\server\\share /delete) or use the same credentials.",
        ["Nessun server di accesso disponibile per il dominio."] = "No logon server available for the domain.",
        ["Formato data vuoto."] = "Empty date format.",
        ["Il formato data non può contenere ore/minuti/secondi."] = "The date format cannot contain hours/minutes/seconds.",
        ["Formato data non valido: {0}."] = "Invalid date format: {0}.",
        ["Il formato data produce caratteri non ammessi nel percorso."] = "The date format produces characters not allowed in a path.",
        ["Share nel formato \\\\server\\share."] = "Share must look like \\\\server\\share.",
        ["La data non è ammessa nella share."] = "A date is not allowed in the share.",
        ["File obbligatorio."] = "File required.",
        ["Il percorso UNC del file deve essere dentro la share."] = "The file's UNC path must be inside the share.",
        ["Righe iniziali tra 0 e 100000."] = "Initial lines must be between 0 and 100000.",
        ["File sessioni corrotto, salvato come {0}.bak."] = "Sessions file corrupt, saved as {0}.bak.",
        ["File non valido."] = "Invalid file.",
        ["File non riconosciuto."] = "File not recognized.",
        ["Versione del file non supportata."] = "File version not supported.",
        ["Una collezione non può essere spostata dentro sé stessa."] = "A collection cannot be moved into itself.",

        // ---- advanced search ----
        ["Cerca…"] = "Search…",
        ["Ricerca avanzata tra tutte le sessioni aperte (Ctrl+Maiusc+F)"] = "Advanced search across all open sessions (Ctrl+Shift+F)",
        ["Ricerca avanzata"] = "Advanced search",
        ["Cerca"] = "Search",
        ["Testo da cercare"] = "Text to find",
        ["Maiuscole/minuscole"] = "Match case",
        ["Parola intera"] = "Whole word",
        ["Livelli"] = "Levels",
        ["Da"] = "From",
        ["A"] = "To",
        ["Da (data)"] = "From (date)",
        ["Da (ora)"] = "From (time)",
        ["A (data)"] = "To (date)",
        ["A (ora)"] = "To (time)",
        ["Ora: HH:mm o HH:mm:ss"] = "Time: HH:mm or HH:mm:ss",
        ["Esporta…"] = "Export…",
        ["Salva i risultati in un file"] = "Save the results to a file",
        ["Esporta risultati"] = "Export results",
        ["Risultati"] = "Results",
        ["Indica anche la data"] = "Enter the date too",
        ["Ora non valida (usa HH:mm o HH:mm:ss)"] = "Invalid time (use HH:mm or HH:mm:ss)",
        ["Inserisci un testo o un filtro"] = "Enter some text or a filter",
        ["Nessuna sessione aperta"] = "No open sessions",
        ["Ricerca in corso…"] = "Searching…",
        ["Ricerca annullata"] = "Search cancelled",
        ["Espressione regolare non valida: {0}"] = "Invalid regular expression: {0}",
        ["Nessun risultato"] = "No results",
        ["{0} risultati in {1} sessioni"] = "{0} results in {1} sessions",
        ["Risultati limitati a {0:N0}: restringi la ricerca"] = "Results limited to {0:N0}: narrow the search",
        ["La riga non è più disponibile nel buffer"] = "The line is no longer in the buffer",
        ["Filtri rimossi"] = "Filters cleared",

        // ---- open local file ----
        ["Apri file…"] = "Open file…",
        ["Apri un file di log (Ctrl+O)"] = "Open a log file (Ctrl+O)",
        ["Apri file di log"] = "Open log file",
        ["Log e testo (*.log;*.txt)|*.log;*.txt|Tutti i file (*.*)|*.*"] = "Log and text (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*",
        ["Impossibile aprire «{0}»: {1}"] = "Cannot open \"{0}\": {1}",
        ["Nessun file valido"] = "No valid file",
    };
}

/// XAML: `Content="{local:T '_Nome'}"`. The argument is the Italian text.
public class TExtension(string it) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => AsBinding().ProvideValue(serviceProvider);

    // Re-evaluated whenever LocSource.Version changes, i.e. on every language switch.
    internal Binding AsBinding() => new("Version") { Source = LocSource.Instance, Converter = Conv.Instance, ConverterParameter = it, Mode = BindingMode.OneWay };

    private sealed class Conv : IValueConverter
    {
        public static readonly Conv Instance = new();
        object IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => L.T((string)parameter!);
        object IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}

internal sealed class LocSource : INotifyPropertyChanged
{
    public static readonly LocSource Instance = new();
    public int Version { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Bump() { Version++; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version))); }
}
