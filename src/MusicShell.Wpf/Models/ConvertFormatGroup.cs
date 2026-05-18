using System.Collections.ObjectModel;

namespace MusicShell.Models;

public sealed class ConvertFormatGroup
{
    public ConvertFormatGroup(string name, IEnumerable<string> formats)
    {
        Name = name;
        Formats = new ObservableCollection<string>(formats);
    }

    public string Name { get; }
    public ObservableCollection<string> Formats { get; }
}
