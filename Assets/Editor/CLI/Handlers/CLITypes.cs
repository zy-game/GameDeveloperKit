using System.Collections.Generic;

namespace GameDeveloperKit.Editor.CLI
{
    public interface ICLIHandler
    {
        List<string> GetCommands();
        string Execute(string command, string parameters);
    }
}
