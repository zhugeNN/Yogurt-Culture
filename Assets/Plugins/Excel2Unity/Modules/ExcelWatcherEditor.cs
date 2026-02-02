#if UNITY_EDITOR
using System.IO;
using UnityEditor;
namespace Excel2Unity
{

  public class ExcelWatcherEditor
  {
    private static FileSystemWatcher watcher;
    private static string pathToWatch = ExcelConfig.ExcelTablePath; // 要监控的目录路径

    [InitializeOnLoadMethod]
    // [InitializeOnLoadMethod]用于指定一个方法，该方法将在Unity编辑器启动时加载该脚本所在的汇编后立即执行
    private static void Initialize()
    {
      // 创建FileSystemWatcher实例
      if (watcher != null) return;
      watcher = new FileSystemWatcher
      {
        Path = pathToWatch,
        Filter = "*.xlsx", // 监控Excel文件
        NotifyFilter = NotifyFilters.LastWrite // 监控文件最后写入时间
      };
      // 当文件被修改时触发的事件
      watcher.Changed += OnChanged;
      // 开始监控
      watcher.EnableRaisingEvents = true;

      // 监听Unity编辑器退出事件
      EditorApplication.quitting += StopWatching;
      // Debug.Log("Excel文件监控已启动");
    }

    private static void OnChanged(object source, FileSystemEventArgs e)
    {
      //Debug.Log("文件发生更改:" + e.FullPath);
      TriggerExcelToJson(e.FullPath);
    }
    private static void TriggerExcelToJson(string filePath)
    {
      if (filePath.EndsWith(".xlsx"))
      {
        var excelName = Path.GetFileNameWithoutExtension(filePath);
        //Debug.Log("触发保存自动导表：" + excelName);
        // 调用你的JSON导表流程
        ExcelToCSharpClass.ExcelFileConvertToJson(filePath);
      }
    }


    // [DidReloadScripts]
    // private static void OnScriptsReloaded()
    // {
    //   StopWatching();
    //   // 当脚本重新加载时，重新初始化监控
    //   Initialize();
    // }

    public static void StopWatching()
    {
      if (watcher != null)
      {
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        watcher = null;
        Excel2UnityUtils.Log("Excel文件监控已关闭");
      }
    }
  }
}
#endif