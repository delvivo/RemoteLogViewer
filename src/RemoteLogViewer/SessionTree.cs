using System.IO;

namespace RemoteLogViewer;

/// A folder of saved sessions. Nesting via ParentId (null = root).
public class SessionCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Guid? ParentId { get; set; }
}

/// Portable file for export/import: same shape as sessions.json, never with passwords.
public class ExportFile
{
    public const string FormatId = "remote-log-viewer-collection";
    public string? Format { get; set; }
    public int Version { get; set; }
    public List<SessionCollection> Collections { get; set; } = [];
    public List<SessionConfig> Sessions { get; set; } = [];
}

/// Saved sessions and collections as flat lists; the tree is derived (ParentId / CollectionId).
public class SessionTree(List<SessionCollection> collections, List<SessionConfig> sessions)
{
    public SessionTree() : this([], []) { }

    public List<SessionCollection> Collections { get; } = collections;
    public List<SessionConfig> Sessions { get; } = sessions;

    private static readonly StringComparer NameOrder = StringComparer.CurrentCultureIgnoreCase;

    public IEnumerable<SessionCollection> ChildCollections(Guid? parentId) =>
        Collections.Where(c => c.ParentId == parentId).OrderBy(c => c.Name, NameOrder);

    public IEnumerable<SessionConfig> ChildSessions(Guid? parentId) =>
        Sessions.Where(s => s.CollectionId == parentId).OrderBy(s => s.Name, NameOrder);

    public SessionCollection? Find(Guid id) => Collections.FirstOrDefault(c => c.Id == id);

    /// All collections below `id` (not including it).
    public List<SessionCollection> Descendants(Guid id)
    {
        var result = new List<SessionCollection>();
        var queue = new Queue<Guid>([id]);
        while (queue.TryDequeue(out var p))
            foreach (var c in Collections.Where(c => c.ParentId == p && c.Id != id && !result.Contains(c)))
            {
                result.Add(c);
                queue.Enqueue(c.Id);
            }
        return result;
    }

    private HashSet<Guid> SubtreeIds(Guid id) => [id, .. Descendants(id).Select(c => c.Id)];

    public bool CanMove(Guid collectionId, Guid? newParentId) =>
        newParentId is not { } p || !SubtreeIds(collectionId).Contains(p);

    public void Move(SessionCollection c, Guid? parentId)
    {
        if (!CanMove(c.Id, parentId)) throw new InvalidOperationException("Una collezione non può essere spostata dentro sé stessa.");
        if (c.ParentId == parentId) return;
        c.Name = UniqueName(parentId, c.Name, c.Id);
        c.ParentId = parentId;
    }

    public void Move(SessionConfig s, Guid? parentId) => s.CollectionId = parentId;

    public int CountSessions(Guid id)
    {
        var ids = SubtreeIds(id);
        return Sessions.Count(s => s.CollectionId is { } c && ids.Contains(c));
    }

    public int CountCollections(Guid id) => Descendants(id).Count;

    public void Delete(Guid id)
    {
        var ids = SubtreeIds(id);
        Collections.RemoveAll(c => ids.Contains(c.Id));
        Sessions.RemoveAll(s => s.CollectionId is { } c && ids.Contains(c));
    }

    public bool NameTaken(Guid? parentId, string name, Guid? exceptId = null) =>
        Collections.Any(c => c.ParentId == parentId && c.Id != exceptId && string.Equals(c.Name, name, StringComparison.CurrentCultureIgnoreCase));

    public string UniqueName(Guid? parentId, string name, Guid? exceptId = null) =>
        Unique(name, n => NameTaken(parentId, n, exceptId));

    private string UniqueSessionName(Guid? parentId, string name) =>
        Unique(name, n => Sessions.Any(s => s.CollectionId == parentId && string.Equals(s.Name, n, StringComparison.CurrentCultureIgnoreCase)));

    private static string Unique(string name, Func<string, bool> taken)
    {
        if (!taken(name)) return name;
        for (var i = 2; ; i++)
            if (!taken($"{name} ({i})")) return $"{name} ({i})";
    }

    public string PathOf(Guid id)
    {
        var names = new List<string>();
        for (var c = Find(id); c != null && names.Count <= Collections.Count; c = c.ParentId is { } p ? Find(p) : null)
            names.Insert(0, c.Name);
        return string.Join(" › ", names);
    }

    /// Repairs a hand-edited file: orphans go to root, a cycle is broken at the collection that closes it.
    public void Normalize()
    {
        var ids = Collections.Select(c => c.Id).ToHashSet();
        foreach (var c in Collections.Where(c => c.ParentId is { } p && !ids.Contains(p))) c.ParentId = null;
        foreach (var s in Sessions.Where(s => s.CollectionId is { } p && !ids.Contains(p))) s.CollectionId = null;
        foreach (var c in Collections)
        {
            var seen = new HashSet<Guid> { c.Id };
            for (var p = c.ParentId; p is { } pid; p = Find(pid)!.ParentId)
                if (!seen.Add(pid)) { if (pid == c.Id) c.ParentId = null; break; }
        }
    }

    // ---- export / import ----

    /// `collectionId` null = everything. Passwords are never exported.
    public ExportFile Export(Guid? collectionId)
    {
        List<SessionCollection> cols = collectionId is { } id ? [Find(id)!, .. Descendants(id)] : Collections;
        var colIds = cols.Select(c => c.Id).ToHashSet();
        var sessions = collectionId == null ? Sessions : Sessions.Where(s => s.CollectionId is { } c && colIds.Contains(c));
        return new ExportFile
        {
            Format = ExportFile.FormatId,
            Version = 1,
            Collections = cols.Select(c => new SessionCollection { Id = c.Id, Name = c.Name, ParentId = c.Id == collectionId ? null : c.ParentId }).ToList(),
            Sessions = sessions.Select(s => { var x = s.Clone(); x.ProtectedPassword = null; return x; }).ToList(),
        };
    }

    /// Adds the file's content under `targetParentId` with new ids; never touches existing items.
    /// Returns the first imported root item (collection or session), or null if the file is empty.
    public object? Import(ExportFile file, Guid? targetParentId)
    {
        Validate(file);
        var map = file.Collections.ToDictionary(c => c.Id, _ => Guid.NewGuid());
        object? first = null;

        foreach (var c in file.Collections)
        {
            var parent = c.ParentId is { } p ? map[p] : targetParentId;
            var copy = new SessionCollection { Id = map[c.Id], ParentId = parent, Name = c.ParentId == null ? UniqueName(parent, c.Name) : c.Name };
            Collections.Add(copy);
            if (c.ParentId == null) first ??= copy;
        }
        foreach (var s in file.Sessions)
        {
            var copy = s.Clone();
            copy.Id = Guid.NewGuid();
            copy.ProtectedPassword = null;
            copy.CollectionId = s.CollectionId is { } c ? map[c] : targetParentId;
            if (s.CollectionId == null) copy.Name = UniqueSessionName(targetParentId, copy.Name);
            Sessions.Add(copy);
            if (s.CollectionId == null) first ??= copy;
        }
        return first;
    }

    private static void Validate(ExportFile file)
    {
        var ids = new HashSet<Guid>();
        var ok = file.Collections.All(c => ids.Add(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
            && file.Collections.All(c => c.ParentId is not { } p || ids.Contains(p))
            && file.Sessions.All(s => s.CollectionId is not { } c || ids.Contains(c))
            && file.Sessions.Select(s => s.Id).Distinct().Count() == file.Sessions.Count;
        if (ok)
        {
            // no cycles: walking up from any collection must reach the root within Count steps
            var byId = file.Collections.ToDictionary(c => c.Id);
            ok = file.Collections.All(c =>
            {
                var steps = 0;
                for (var p = c.ParentId; p is { } pid; p = byId[pid].ParentId)
                    if (++steps > byId.Count) return false;
                return true;
            });
        }
        if (!ok) throw new InvalidDataException("File non valido.");
    }
}
