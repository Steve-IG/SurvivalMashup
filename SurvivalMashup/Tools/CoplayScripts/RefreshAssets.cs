using UnityEditor;
using UnityEditor.Compilation;

public static class RefreshAssets
{
    public static string Execute()
    {
        AssetDatabase.Refresh();
        CompilationPipeline.RequestScriptCompilation();
        return "Refresh + compile requested.";
    }
}
