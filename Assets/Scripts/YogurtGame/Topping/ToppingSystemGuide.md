# Topping 系统使用指南

## 📋 概述

Topping 系统实现了从操作台点击生成配料装饰，拖拽到酸奶碗中添加配料的功能。

## 📁 文件结构

```
Assets/Scripts/YogurtGame/Topping/
├── Topping.cs              # Topping基类（抽象类）
├── ToppingSpawner.cs       # Topping生成器（操作台点击）
└── BaseTopping.cs          # 基础Topping实现示例
```

## 🔧 核心组件

### 1. Topping.cs - Topping基类

**功能：**
- 实现拖拽功能（使用 IDragHandler）
- 鼠标松开后检测是否与Ingredient重合
- 如果重合，添加到Ingredient；否则销毁
- 持续记录状态（无论是否隐藏）

**关键方法：**
- `LoadTopping()` - 虚函数，子类实现具体加载逻辑
- `Show()` / `Hide()` - 显示/隐藏Topping
- `GetState()` / `SetState()` - 获取/设置状态数据
- `UpdateState()` - 持续更新状态（在Update中调用）

**必需组件：**
- `Collider2D`（isTrigger = false，用于拖拽检测）

### 2. ToppingSpawner.cs - Topping生成器

**功能：**
- 从操作台点击后生成Topping实体
- 支持在鼠标位置或指定位置生成

**使用方法：**
1. 将脚本挂载到操作台GameObject上
2. 设置 `toppingPrefab`（要生成的Topping Prefab）
3. 配置生成位置和父节点（可选）

**必需组件：**
- `Collider2D`（isTrigger = false，用于点击检测）

### 3. BaseTopping.cs - 示例实现

**功能：**
- 展示如何继承Topping基类
- 实现 `LoadTopping()` 虚函数
- 提供基础的状态管理示例

## 🔄 工作流程

### 1. 生成Topping
```
用户点击操作台（ToppingSpawner）
  ↓
OnPointerClick 触发
  ↓
实例化 Topping Prefab
  ↓
Topping 跟随鼠标拖拽
```

### 2. 添加Topping到Ingredient
```
用户松开鼠标（OnEndDrag）
  ↓
检测与Ingredient图层是否重合
  ↓
如果重合：
  - 调用 ingredient.AddTopping(this)
  - 隐藏Topping（不销毁）
  - 设置为Ingredient的子对象
如果未重合：
  - 销毁Topping实体
```

### 3. Topping显示/隐藏
```
Ingredient放大时（IngredientController）
  ↓
调用 ingredient.ShowToppings()
  ↓
首次放大：加载Topping（LoadToppings）
  ↓
显示所有Topping（topping.Show()）

Ingredient缩小时
  ↓
调用 ingredient.HideToppings()
  ↓
隐藏所有Topping（topping.Hide()）
```

## 📝 使用步骤

### 步骤1: 创建Topping Prefab

1. 创建GameObject，添加必要的组件：
   - `Collider2D`（isTrigger = false）
   - `SpriteRenderer`（或其他渲染组件）
   - 继承自 `Topping` 的脚本（如 `BaseTopping`）

2. 配置Topping脚本：
   - 设置 `ingredientLayerName`（Ingredient的Layer名称）
   - 配置拖拽设置

3. 保存为Prefab

### 步骤2: 设置操作台

1. 创建操作台GameObject
2. 添加 `Collider2D`（isTrigger = false）
3. 添加 `ToppingSpawner` 组件
4. 在Inspector中设置：
   - `Topping Prefab` = 步骤1创建的Prefab
   - `Spawn At Mouse Position` = true（推荐）

### 步骤3: 配置Ingredient

1. 在Ingredient的Inspector中，找到 "Topping管理" 部分
2. 设置 `Topping Data List`：
   - 添加Topping数据项
   - 为每个项设置：
     - `Topping Prefab`（用于首次放大时加载）
     - `Topping Type`（类型标识）
     - `Local Position`（相对于Ingredient的位置）
     - `Local Rotation`（旋转）
     - `Local Scale`（缩放）

### 步骤4: 配置Layer

1. 在 Project Settings > Tags and Layers 中创建Layer：
   - 为Ingredient创建专用Layer（例如："Ingredient"）
   - 为Topping设置 `ingredientLayerName` 为该Layer名称

## 🎯 关键特性

### 1. 状态持续记录

Topping脚本在 `Update()` 中持续调用 `UpdateState()`，无论Topping是否隐藏，都会记录状态：
- 位置、旋转、缩放
- 是否正在拖拽
- 是否已添加到Ingredient
- 自定义状态数据

### 2. 虚函数设计

`LoadTopping()` 是虚函数，子类可以实现：
- 加载材质、贴图
- 初始化动画
- 设置数值
- 其他自定义逻辑

### 3. 自动管理

- Ingredient自动管理Topping列表
- 自动在放大时显示，缩小时隐藏
- 首次放大时自动加载预设的Topping

## 💡 示例代码

### 创建自定义Topping

```csharp
public class FruitTopping : Topping
{
    [SerializeField] private Sprite fruitSprite;
    [SerializeField] private float sweetness = 0.5f;
    
    public override void LoadTopping()
    {
        // 设置贴图
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && fruitSprite != null)
        {
            sr.sprite = fruitSprite;
        }
        
        // 记录状态
        stateData["sweetness"] = sweetness;
        stateData["fruitType"] = "Apple";
    }
}
```

### 从代码添加Topping

```csharp
// 在运行时动态添加Topping
Ingredient ingredient = GetComponent<Ingredient>();
ToppingData newTopping = new Ingredient.ToppingData
{
    toppingPrefab = fruitToppingPrefab,
    toppingType = "Fruit",
    localPosition = new Vector3(0, 0.5f, 0),
    localRotation = Quaternion.identity,
    localScale = Vector3.one
};

List<Ingredient.ToppingData> toppings = ingredient.GetToppingDataList();
toppings.Add(newTopping);
ingredient.SetToppingDataList(toppings);
```

## ⚠️ 注意事项

1. **Collider2D设置**
   - Topping的Collider2D必须 `isTrigger = false`（用于拖拽）
   - ToppingSpawner的Collider2D必须 `isTrigger = false`（用于点击）

2. **Layer配置**
   - 确保Ingredient的Layer正确配置
   - Topping的 `ingredientLayerName` 必须匹配Ingredient的Layer

3. **Prefab设置**
   - Topping Prefab必须包含继承自Topping的脚本
   - 确保Prefab有Collider2D组件

4. **状态记录**
   - Topping会持续记录状态，即使隐藏也会更新
   - 可以通过 `GetState()` 查询状态

## 🔍 调试建议

1. **检查EventSystem**
   - 确认场景中有EventSystem（脚本会自动创建）
   - 确认Camera有Physics2DRaycaster（脚本会自动添加）

2. **检查Layer**
   - 确认Ingredient的Layer已配置
   - 确认Topping的 `ingredientLayerName` 正确

3. **检查Collider**
   - 确认所有Collider2D的isTrigger设置正确
   - 确认Collider大小合适

4. **检查Prefab**
   - 确认Topping Prefab有Topping脚本
   - 确认Prefab结构正确

## 📚 相关脚本

- `Ingredient.cs` - 管理Topping列表
- `IngredientController.cs` - 控制Topping显示/隐藏
- `StickController.cs` - 工具拖拽（参考实现）

