# Excel2Unity 使用指南

Excel2Unity 是一个 Unity 编辑器插件，用于将 Excel 表格数据转换为 Unity 可用的 JSON 数据和 C# 脚本类。

## 目录结构

```
Assets/Plugins/Excel2Unity/
├── dll/                    # 依赖的 DLL 文件
├── Modules/               # 核心模块
│   ├── ExcelConfig.cs     # 配置类，定义路径
│   ├── ExcelXmlConfig.xml # XML 配置文件
│   ├── XmlConfigLoader.cs # XML 配置加载器
│   ├── ExcelToCSharpClass.cs # 主要的转换逻辑
│   ├── DataTableUtils.cs  # 数据表读取工具
│   └── TableDataBase.cs   # 数据表基类
└── README.md             # 本文档
```

## 前期准备

### 1. Excel 文件放置位置

将 Excel 文件放置在以下目录：
```
Assets/Main/DataTable/Excel/
```

插件会在运行时自动创建该目录（如果不存在）。

### 2. Excel 文件格式要求

Excel 表格需要遵循特定的格式：

#### 表格结构示例：

| 字段描述 | ID | 名称 | 等级 | 是否激活 |
|---------|----|----|----|--------|
| 字段名   | ID | Name | Level | IsActive |
| 字段类型 | int | string | int | bool |
| 数据行1  | 1 | 英雄A | 10 | 1 |
| 数据行2  | 2 | 英雄B | 15 | 0 |

#### 格式说明：
- **第1行**：字段描述（可选，用于备注）
- **第2行**：字段名（必须，与生成的 C# 类属性名对应）
- **第3行**：字段类型（必须，支持：int, string, float, double, bool, List<T>, enum等）
- **第4行开始**：实际数据行
- **第一列默认为ID列**：如果第一列为空，该行将被跳过

#### 支持的数据类型：
- 基本类型：`int`, `string`, `float`, `double`, `bool`
- 枚举类型：支持项目中定义的任何 enum
- 列表类型：`List<int>`, `List<string>` 等（用逗号或分号分隔）
- 自定义类型：支持项目中定义的任何类

### 3. XML 配置关联

在 `Modules/ExcelXmlConfig.xml` 中为每个 Excel 文件添加配置项：

```xml
<ExcelXmlConfiguration>
  <ExcelXmlConfigs>
    <ExcelXmlConfig>
      <ExcelPath>HeroData.xlsx</ExcelPath>          <!-- Excel文件名 -->
      <JsonPath>HeroData.json</JsonPath>            <!-- 输出JSON文件名 -->
      <WorksheetName>Sheet1</WorksheetName>         <!-- Excel工作表名 -->
      <ClassName>HeroData</ClassName>               <!-- 生成的C#类名 -->
      <SOPath>Heros/</SOPath>                       <!-- 可选：SO文件路径 -->
      <SOClassName>HeroDataSO</SOClassName>         <!-- 可选：SO类名 -->
    </ExcelXmlConfig>
  </ExcelXmlConfigs>
</ExcelXmlConfiguration>
```

#### 配置项说明：
- **ExcelPath**: Excel 文件名（相对于 ExcelTablePath）
- **JsonPath**: 输出 JSON 文件名
- **WorksheetName**: Excel 中的工作表名称
- **ClassName**: 生成的 C# 类名
- **SOPath**: 可选，ScriptableObject 文件路径
- **SOClassName**: 可选，ScriptableObject 类名

**注意**：一个 Excel 文件可以有多个工作表，每个工作表对应一个配置项。

## 使用方法

### 1. 转换单个 Excel 文件

1. 在 Project 窗口中选中要转换的 Excel 文件
2. 右键点击，选择以下菜单项之一：
   - **Assets/ExcelTool/ExcelToJson**: 只生成 JSON 文件
   - **Assets/ExcelTool/ExcelToJsonAndClass**: 生成 JSON 文件和 C# 类
   - **Assets/ExcelTool/ExcelToJsonAndForceUpdateClass**: 强制更新 C# 类（覆盖现有文件）

### 2. 批量转换文件夹

1. 在 Project 窗口中选中包含 Excel 文件的文件夹
2. 右键点击，选择上述菜单项之一
3. 系统会自动处理文件夹中的所有 `.xlsx` 文件

### 3. 运行时读取数据

在代码中使用转换后的数据：

```csharp
using Excel2Unity;

// 读取单个数据项
HeroData hero = DataTableUtils.LoadDataTable<HeroData>("1");

// 读取所有数据
Dictionary<string, HeroData> allHeroes = DataTableUtils.DeserializeJsonData<HeroData>();
```

## 输出结果

### 1. 输出位置

| 输出类型 | 默认位置 | 说明 |
|---------|---------|------|
| JSON 文件 | `Assets/Resources/DataTable/JsonData/` | 通过 Resources.Load 加载 |
| C# 类文件 | `Assets/Scripts/Excel/` | 自动生成的脚本类 |
| SO 文件 | `Assets/Resources/SO/` | 可选的 ScriptableObject 文件 |

### 2. 输出格式

#### JSON 文件格式：
```json
[
  {
    "ID": "1",
    "Name": "英雄A",
    "Level": 10,
    "IsActive": true
  },
  {
    "ID": "2",
    "Name": "英雄B",
    "Level": 15,
    "IsActive": false
  }
]
```

#### C# 类格式：
```csharp
using System;
using UnityEngine;
using Excel2Unity;
using System.Collections.Generic;

[Serializable]
public class HeroData : TableDataBase
{
    public int ID;          // ID 字段在基类中已定义
    public string Name;
    public int Level;
    public bool IsActive;
}
```

### 3. 调整输出位置

如需修改输出路径，请编辑 `Modules/ExcelConfig.cs`：

```csharp
public class ExcelConfig
{
    // Excel 文件存放目录
    private static string excelTablePath = "Main/DataTable/Excel";

    // JSON 文件输出目录（相对于 Assets）
    private static string jsonDataPath = "Resources/DataTable/JsonData";

    // C# 类文件输出目录（相对于 Assets）
    private static string cSharpPath = "Scripts/Excel";

    // 其他路径配置...
}
```

**注意**：
- 修改路径后，需要重新运行转换
- JSON 路径会影响运行时的 `Resources.Load` 路径
- 确保目标目录存在且有写入权限

## 常见问题

### 1. Excel 文件无法识别
- 检查文件是否为 `.xlsx` 格式
- 确保文件未被其他程序锁定
- 检查文件路径是否正确

### 2. XML 配置错误
- 确保 `ExcelXmlConfig.xml` 格式正确
- 检查 Excel 文件名是否与配置中的 `ExcelPath` 匹配
- 工作表名称是否与 `WorksheetName` 匹配

### 3. 数据类型转换失败
- 检查 Excel 中的数据格式是否与字段类型匹配
- 对于 List 类型，确保用逗号或分号分隔
- 对于 enum 类型，确保值在枚举定义中存在

### 4. 运行时无法加载数据
- 确保 JSON 文件在 Resources 文件夹中
- 检查 `Resources.Load` 的路径是否正确
- 确认数据 ID 存在

## 依赖说明

- **EPPlus.dll**: Excel 文件读写库
- **LitJSON.dll**: JSON 序列化库
- **Newtonsoft.Json.dll**: JSON 处理库（运行时使用）

## 版本兼容性

- Unity 2019.4+
- .NET Framework 4.6+
- Excel 2007+ (.xlsx 格式)
