# 制作 → 下单 → 交付流程依赖说明

本文档说明从 NPC 生成到提交订单整个链路中，脚本与编辑器配置的依赖关系，便于检查或排错。

## 1. NPC 生成
- **脚本**：`NpcManager`
- **依赖配置**：
  - `npcPrefabs`：至少包含一个带 `NpcController`、`Animator`、`Collider2D`/`Collider` 的 NPC 预制体。
  - `yogurtShop`：队列触发区的 `GameObject`，需带 `Collider2D`（触发器）。
  - `queueParent`：队列根节点，用于重新挂载排队 NPC。
  - `windowTransform`：排队时坐标基准点。
  - `queueStepOffset`：队列间距（负值表示向左递进，`LeaveQueue` 会使用 `Abs` 作补位距离）。
  - `npcRoot`（可选）：NPC 统一父节点，不配置会在运行时创建。

## 2. NPC 触发进入队列
- **脚本**：`NpcController`
- **依赖配置**：
  - `NpcController.ConfigureQueueTargets(...)` 在 NPC 生成后由 `NpcManager` 设置上述四个引用。
  - NPC 上需有 `Collider2D`（触发触发器）并勾选 `IsTrigger` 或搭配 `OnTriggerEnter2D`。
  - `Animator` 需包含 `IsWalking` Bool 参数，用于切换 walk/idle。

## 3. 进入队列 → 生成订单
- **脚本**：`NpcController`、`OrderManager`
- **依赖配置**：
  - `QueueParent` 子节点顺序决定 NPC 排队位置，`queueStepOffset` 决定横向间隔。
  - `OrderManager` 单例需要在场景中，`orderPrefabs` 列表中至少配置一个订单预制体（带 `ShopItem`）。
  - NPC 进入队列达成定位后自动调用 `OrderManager.AddOrder()` 进入订单队列。

## 4. 订单实例化
- **脚本**：`OrderManager`
- **依赖配置**：
  - `OrderPos`：UI/世界中显示订单的目标位置 Transform。
  - `OrderRoot`：实例化订单实体的父节点。
  - 订单预制体需带 `ShopItem`，并在 Inspector 中配置其 `Ingredients` 列表（模板数据）。

## 5. 进度条（Progress）添加与完成
- **脚本**：`ProgressController`、`Ingredient` 派生脚本（如 `NormalYogurt`）
- **依赖配置**：
  - `ProgressController.sliderPrefabs`：所有可用 Slider 预制体，需挂 `Ingredient`。
  - `sliderContainer`：运行时放置 Slider 的父节点。
  - `spawnPoint`：制作完成后成品（`ShopItem`）实例化位置。未配置时默认挂在 `sliderContainer`。
  - `Ingredient.prefab`：完成后要实例化的 `ShopItem` 预制体引用。

## 6. 生产出的 ShopItem 拖拽
- **脚本**：`ShopItem`
- **依赖配置**：
  - `orderLayerName`：拖拽结束时检测的 Layer（默认 `order`）。需要在 Project Settings → Tags and Layers 中设置并赋予订单接收区域的 Collider2D。
  - ShopItem 必须带 `Collider2D`，接收区域 Collider2D 必须位于 `orderLayerName` 指定层。

## 7. 订单匹配与提交通知
- **脚本**：`OrderManager.HandleOrderSubmit`, `MatchOrder`
- **依赖配置**：
  - 订单模板 `ShopItem.Ingredients` 与玩家制作 `ShopItem.GetIngredients()` 按顺序一一对应（数量、`Ingredient` 类型均需一致）才能通过 `MatchOrder`。
  - 成功时 `SubmitSuccess()` 销毁当前订单实例、调用 `NpcManager.LeaveQueue()` 让队首 NPC 离队，并触发队列补位。

## 8. NPC 离队
- **脚本**：`NpcManager.LeaveQueue`, `NpcController.LeaveQueueAndResume`
- **依赖配置**：
  - `NpcManager` 的 `queuedNPC` 会随着订单完成出队；剩余 NPC 通过 `ShiftInQueue` 以 0.2 速度向右补位 `Abs(queueStepOffset)` 距离。
  - `NpcController` 会恢复进入队列前记录的移动方向、速度和 walk/idle 协程。

按照以上依赖配置即可串起“NPC 生成 → 排队 → 产单 → 制作 → 提交 → 下一单”的完整流程。

---

## 类命名优化建议

根据游戏逻辑梳理，当前部分类名与职责不够清晰。以下是重命名建议，以提升代码可读性和维护性：

### 1. NPC 相关类

**`NpcController` → `CustomerController` 或 `CustomerBehavior`**
- **理由**：在酸奶店游戏中，NPC 实际是"顾客"，使用 `Customer` 更符合业务语义
- **职责**：控制单个顾客的行为（移动、动画、进入/离开队列）

**`NpcManager` → `CustomerSpawner` 或 `CustomerManager`**
- **理由**：与 `CustomerController` 保持一致，明确表示这是顾客的生成和管理系统
- **职责**：管理所有顾客的生成、回收、队列管理

### 2. 制作相关类

**`ProgressController` → `YogurtMaker` 或 `ProductionController`**
- **理由**：当前名称过于泛化，实际职责是管理酸奶制作流程（添加配料、执行制作、完成）
- **职责**：控制制作流程，管理 Slider 进度条和配料执行

**`Ingredient` → 保持或改为 `YogurtIngredient`**
- **理由**：如果游戏只有酸奶相关配料，可加前缀；否则保持通用性
- **职责**：配料基类，定义制作逻辑接口

**`NormalYogurt` → 保持或改为 `BasicYogurtIngredient`**
- **理由**：当前命名已较清晰，如需强调是配料可加后缀
- **职责**：普通酸奶配料的制作逻辑实现

### 3. 产品相关类

**`ShopItem` → `YogurtProduct` 或 `FinishedYogurt`**
- **理由**：当前名称过于泛化，实际是制作完成的酸奶产品，包含配料信息
- **职责**：制作完成的成品，可拖拽提交给订单系统

### 4. 订单相关类

**`OrderManager` → 保持 `OrderManager`**
- **理由**：命名已清晰，职责明确
- **职责**：管理订单的创建、匹配、完成

### 重命名优先级

**高优先级**（影响理解）：
1. `NpcController` → `CustomerController`
2. `NpcManager` → `CustomerManager`
3. `ShopItem` → `YogurtProduct`
4. `ProgressController` → `YogurtMaker`

**中优先级**（可选优化）：
5. `Ingredient` → `YogurtIngredient`（如果确定只有酸奶配料）

**低优先级**（已较清晰）：
6. `NormalYogurt` → 保持或微调
7. `OrderManager` → 保持

### 注意事项

- 重命名时需要同步更新所有引用（包括 Inspector 中的配置）
- 建议使用 IDE 的重构工具（如 Visual Studio 的 Rename）批量替换
- 重命名后需要更新本文档中的类名引用


## 改进建议（设计模式与流程优化）

- **事件/发布订阅解耦**：引入轻量 EventBus（C# 事件或 ScriptableObject Event），分发“订单生成/进度开始/进度完成/订单提交”等事件，减少 `NpcManager`、`OrderManager`、`ProgressController`、`IngredientController` 之间的直接依赖。
- **状态机驱动制作与拖拽**：为 `IngredientController` / `StickController` / `YogurtProduct` 建立简单 FSM（Idle→Dragging→Enlarged→ProgressRunning→Shrinking→Done），把动画、光标限制、进度启动/结束的条件集中到状态切换，降低零散 `Update` 判断。
- **策略/接口封装检测与范围**：将拖拽范围与提交检测抽象为接口（如 `IDragBoundsProvider`、`ISubmitTargetDetector`），当前实现用 BoxCollider2D，未来可无痛替换为圆形、多区域或 UI 区域；`YogurtProduct` 只依赖接口。
- **配置下沉到 ScriptableObject**：把队列/订单/制作参数（速率、曲线、默认缩放、拖拽范围 tag、Layer 名等）放入 ScriptableObject，运行时注入，便于多关卡/调参。
- **对象池健壮性**：为 `ObjectPool` 添加空引用保护与可选预热，取出为空时回退 Instantiate，避免关键对象缺失。
- **单一职责分层**：`ProgressController` 只管进度流程，`IngredientController` 只管触发/动画/光标限制，`Ingredient` 派生类只管进度增量策略；通过事件/接口交互，避免横向耦合。
- **调试可控**：集中调试开关（DebugService/Logger），替代散落的 `Debug.Log`，Inspector 可启用/关闭，便于排错且不污染正式日志。

## 当前实现中职责不清晰或耦合较高的具体位置（按文件 & 代码片段）
下面列出代码中发现的“职责越界”或耦合点，并给出简短说明与改进方向，便于按上述单一职责原则重构。

- IngredientController -> 直接调用 ProgressController.StartProgress
  - 位置（示例）：`IngredientController.OnAnimationComplete()` 调用：
    - Assets/Scripts/YogurtGame/Ingredient/IngredientController.cs L277-L283
  - 问题：IngredientController 触发了进度流程的启动（跨越职责边界），建议改为发出事件（如 `OnIngredientExpanded`），由 ProgressController 订阅并决定是否 StartProgress。

- IngredientController 内部等待并处理进度完成（协程）并在完成时直接 Destroy自身 prefab
  - 位置：`IngredientController.WaitForProgressComplete()` 与 `OnProgressComplete()`（L547-L571, L576-L588）
  - 问题：ProgressController 已是进度流程的拥有者，IngredientController 不应轮询或判断进度完成状态来做中心化清理，建议使用事件回调（ProgressController 在 Finish 时广播 `OnProgressFinished(yogurt)`），IngredientController 订阅后做局部清理。

- ProgressController 在 Finish 时实例化成品 prefab（耦合 UI/生成）并调用 YogurtProduct.SetIngredients
  - 位置：Assets/Scripts/YogurtGame/ProgressController.cs L324-L333
  - 说明：该点本身合理，但应通过事件或工厂接口将“实例化成品”过程解耦（例如 `IProductFactory.Create(productPrefab, parent)`），便于测试与替换。

- BaseTopping 直接读取 ProgressController.Instance（反射字段 angularVelocity）以获取角速度
  - 位置：Assets/Scripts/YogurtGame/Topping/BaseTopping.cs 中 SetIngredient/FixedUpdate 使用 `ProgressController.Instance.GetCurrentYogurtProgress()` 与反射字段（angularVelocityField）
  - 问题：Topping 依赖 ProgressController 的内部状态（反射读取私有字段），建议从 IngredientController 或 ProgressController 公开一个只读接口（例如 `IYogurtRotationProvider.GetAngularVelocity()`），并通过 SetIngredient 注入该接口引用。

- OrderManager 在 SubmitSuccess 中直接调用 NpcManager.LeaveQueue 与 EconomyManager.AddMoney
  - 位置：Assets/Scripts/YogurtGame/OrderManager.cs L135-L145（LeaveQueue）与 L141 (AddMoney)（若存在）
  - 说明：这些调用本质上是复合行为（完成订单 -> 触发离队、结算），可改为 `OnOrderSucceeded` 事件（携带 Order 信息），由 NpcManager / EconomyManager 等订阅并处理各自职责，降低 OrderManager 的横向耦合。

- IngredientManager.CreateIngredient 负责 Instantiate prefab 并立刻添加 IngredientController 与 IngredientCollisionHandler
  - 位置：Assets/Scripts/YogurtGame/Ingredient/IngredientManager.cs L53-L61 与 L137-L155
  - 说明：该方法做了多项工作（创建、装配组件与默认设置），建议把“创建实例”与“配置 controller”拆成两个小函数或通过注入工厂，便于单元测试与替换行为。

- Topping 与 IngredientController 的注册耦合
  - 位置：Topping.CheckAndAddToIngredient()（Assets/Scripts/YogurtGame/Topping/Topping.cs L231-L237）与 IngredientController.AddTopping（Assets/Scripts/YogurtGame/Ingredient/IngredientController.cs L333-L352）
  - 说明：目前是通过直接调用 AddTopping 注册；建议通过接口 `IIngredientRegister` 或事件（如 IngredientController 发布 `OnAcceptTopping`），以便在未来引入验证/策略（如容量限制、重复过滤）。

## 建议的最小改动路线（优先级排序）
1. 在关键交互点引入事件：
   - `IngredientController` 发布 `OnExpanded`（替代直接 StartProgress 调用）
   - `ProgressController` 发布 `OnProgressStarted` / `OnProgressFinished`
   - `OrderManager` 发布 `OnOrderSucceeded` / `OnOrderFailed`
2. 将直接跨模块调用替换为事件订阅（示例：NpcManager 订阅 `OnOrderSucceeded` 而不是被 OrderManager 直接调用）。
3. 导出少量接口（只读或行为接口）用于注入，例如 `IRotationProvider`（供 Topping 获取角速度）、`IProductFactory`（供 ProgressController 实例化成品）。
4. 逐步重构：先在代码中添加事件触发点（不移除旧调用），并在一段时间内同时支持事件与旧逻辑，待消费方迁移完成后再删旧逻辑。

附注：我可以根据你偏好的优先级把某个点（例如将 IngredientController -> ProgressController 的直接调用改为事件）实现成具体代码修改补丁。需我开始重构哪一项？  