 # 背包 UI 创建步骤（超详细版）

第一次做 Unity UI 也没关系，跟着步骤一步步来即可。

---

## 第一步：创建 Canvas

1. 打开 Unity，在 **Hierarchy** 窗口（左侧场景层级）空白处 **右键**
2. 选择 **UI** → **Canvas**
3. 会自动生成两个物体：
   - **Canvas**
   - **EventSystem**（用于处理点击、拖拽等）
4. 选中 **Canvas**，在右侧 **Inspector** 中检查：
   - **Canvas** 组件：Render Mode 为 `Screen Space - Overlay`
   - **Canvas Scaler** 组件：
     - UI Scale Mode：`Scale With Screen Size`
     - Reference Resolution：`1920` x `1080`
     - Screen Match Mode：`Match Width Or Height`
     - Match：`0.5`

> 这样 UI 会随屏幕缩放，不同分辨率都能正常显示。

---

## 第二步：创建背包主面板（InventoryPanel）

1. 在 **Hierarchy** 中，**右键点击 Canvas**
2. 选择 **UI** → **Panel**
3. 会生成一个带灰色背景的 Panel，默认名字可能是 `Panel`
4. 选中它，在 Inspector 顶部把名字改成 **InventoryPanel**
5. 设置 **Rect Transform**（在 Inspector 最上方）：
   - 点击左上角 **Anchor Presets** 方框（四个小三角）
   - 按住 **Alt + Shift**，点击 **中间** 的预设（这样会居中且保持当前大小）
   - **Pos X**：`0`
   - **Pos Y**：`0`
   - **Width**：`400`
   - **Height**：`500`
6. 设置 **Image** 组件（Panel 自带的）：
   - **Color**：点击颜色块，把 Alpha 调到约 `230`（半透明黑底）
7. 在 Inspector 最上方，**取消勾选** InventoryPanel 左边的勾选框，让面板默认隐藏（运行时会用代码打开）

---

## 第三步：创建标题文字

1. 在 **Hierarchy** 中，**右键点击 InventoryPanel**
2. 选择 **UI** → **Text - TextMeshPro**（如果有）
   - 如果弹出 "TMP Importer" 窗口，点 **Import TMP Essentials**
   - 如果没有 TextMeshPro，选 **UI** → **Text** 也可以
3. 重命名为 **TitleText**
4. **Rect Transform**：
   - Anchor：顶部居中（Anchor Presets 选 top-center）
   - Pos Y：`-30`
   - Width：`200`，Height：`40`
5. **Text** 内容：改成 `背包`
6. 字体大小：`24`，颜色：白色，对齐：居中

---

## 第四步：创建格子网格容器（SlotGrid）

1. 在 **Hierarchy** 中，**右键点击 InventoryPanel**
2. 选择 **Create Empty**（创建空物体）
3. 重命名为 **SlotGrid**
4. **Rect Transform**：
   - Anchor：拉伸铺满（Anchor Presets 选 stretch-stretch，按住 Alt+Shift 点一下）
   - Left：`20`，Right：`20`，Top：`80`，Bottom：`80`
   - 这样 SlotGrid 在标题下方、留出边距
5. 选中 SlotGrid，点击 **Add Component**
6. 搜索 **Grid Layout Group**，添加
7. 设置 Grid Layout Group：
   - **Cell Size**：`64` x `64`
   - **Spacing**：`5` x `5`
   - **Start Corner**：`Upper Left`
   - **Start Axis**：`Horizontal`
   - **Child Alignment**：`Upper Left`
   - **Constraint**：`Fixed Column Count`
   - **Constraint Count**：`5`（每行 5 个格子）

---

## 第五步：创建单个格子 Prefab（InventorySlotUI）

### 5.1 创建格子

1. 在 **Hierarchy** 中，**右键点击 SlotGrid**
2. 选择 **UI** → **Image**
3. 重命名为 **InventorySlotUI**
4. **Rect Transform**：
   - 不用改，Grid Layout Group 会自动控制子物体大小
   - 确认 Width、Height 为 64 x 64（或留空让 Grid 控制）
5. **Image** 组件：
   - **Color**：深灰色，如 `(80, 80, 80, 255)` 作为格子背景

### 5.2 添加图标子物体

1. **右键点击 InventorySlotUI** → **UI** → **Image**
2. 重命名为 **Icon**
3. **Rect Transform**：
   - 点击 Anchor Presets，选 **stretch-stretch**（右下角那个）
   - 按住 **Alt + Shift** 点一下，让 Icon 铺满父物体
   - **Left**：`4`，**Right**：`4`，**Top**：`4`，**Bottom**：`4`（留 4 像素边距）
4. **Image** 组件：
   - **Color**：白色 `(255, 255, 255, 255)`
   - **Raycast Target**：取消勾选（避免挡住点击）

### 5.3 添加数量文字

1. **右键点击 InventorySlotUI** → **UI** → **Text - TextMeshPro** 或 **Text**
2. 重命名为 **CountText**
3. **Rect Transform**：
   - Anchor：右下角（Anchor Presets 选 bottom-right）
   - Pos X：`-8`，Pos Y：`8`
   - Width：`40`，Height：`20`
4. **Text** 内容：留空（代码会填）
5. 字体大小：`14`，颜色：白色，对齐：右下

### 5.4 给格子挂脚本并绑定

1. 选中 **InventorySlotUI**，点击 **Add Component**，搜索 `InventorySlotUI`，添加
2. 在 Inspector 的 InventorySlotUI 组件里：
   - **Icon Image**：拖入 Icon 子物体
   - **Count Text**：拖入 CountText 子物体

### 5.5 做成 Prefab

1. 在 **Project** 窗口（下方资源列表）中，找到 `Assets/Scripts/Inventory/` 或任意文件夹
2. **右键** → **Create** → **Folder**，命名为 `Prefabs`（如果还没有）
3. 从 **Hierarchy** 把 **InventorySlotUI** 拖到 Project 的 Prefabs 文件夹里
4. 拖完后，Hierarchy 里的 InventorySlotUI 会变成蓝色（表示是 Prefab 实例）
5. **删除** Hierarchy 里的 InventorySlotUI（SlotGrid 下会变空，没关系，代码会动态生成）
6. 在 Project 里选中 InventorySlotUI Prefab，确认 Inspector 中 **Icon Image** 和 **Count Text** 已绑定（从 Prefab 拖进去）

---

## 第六步：添加负重显示和整理按钮

### 6.1 负重文字

1. **右键点击 InventoryPanel** → **UI** → **Text - TextMeshPro** 或 **Text**
2. 重命名为 **WeightText**
3. **Rect Transform**：放在面板底部，如 Pos Y：`-220`，Width：`200`，Height：`30`
4. 内容先写 `负重: 0/100`，运行后会被代码更新

### 6.2 整理按钮

1. **右键点击 InventoryPanel** → **UI** → **Button - TextMeshPro** 或 **Button**
2. 重命名为 **SortButton**
3. **Rect Transform**：放在 WeightText 旁边或下方，如 Pos Y：`-250`
4. 选中 SortButton 下的 **Text** 子物体，把文字改成 `整理`

---

## 第七步：挂脚本并绑定引用

### 7.1 确认 Inventory 组件

1. 选中场景中的 **Player**（你的玩家物体）
2. 如果没有 **Inventory** 组件，点击 **Add Component**，搜索 `Inventory` 添加
3. 设置 capacity：`20`，maxWeight：`100`
4. 在 Test 里把 **Test Item** 拖一个你创建的 ItemData（如没有就先留空）

### 7.2 给 InventoryPanel 挂 InventoryUI

1. 选中 **InventoryPanel**
2. 点击 **Add Component**，搜索 `InventoryUI`，添加
3. 在 Inspector 的 InventoryUI 组件里，把以下引用拖进去：
   - **Inventory**：拖入 Player 物体（有 Inventory 组件的那个）
   - **Slot UIPrefab**：拖入 Project 里的 InventorySlotUI Prefab
   - **Slot Grid Parent**：拖入 SlotGrid 物体
   - **Weight Text**：拖入 WeightText
   - **Sort Button**：拖入 SortButton

### 7.3 添加按键打开背包（用 InventoryInputHelper）

1. 在 **Hierarchy** 中，**右键 Canvas** → **Create Empty**
2. 重命名为 **InventoryInputHelper**（或随便一个名字）
3. 选中它，**Add Component**，搜索 `InventoryInputHelper`，添加
4. 把 **InventoryPanel** 拖到 **Inventory UI** 槽位（因为 InventoryUI 挂在 Panel 上，拖 Panel 即可自动找到 InventoryUI）

---

## 第八步：测试

1. 点击 Unity 顶部的 **Play** 运行游戏
2. 按 **B** 键，应该能打开/关闭背包面板
3. 在 Player 的 Inventory 组件上 **右键** → **Test Add Item**，看格子是否显示物品
4. 点击 **整理** 按钮，看物品是否重新排序

---

## 常见问题

**Q：按 B 没反应？**  
检查 InventoryInputHelper 的 inventoryUI 是否绑定，以及 InventoryPanel 是否激活。

**Q：格子是空的？**  
先创建几个 ItemData（Project 右键 → Create → Inventory → Item Data），在 Inventory 的 Test Item 里指定一个，再 Test Add Item。

**Q：TextMeshPro 报错？**  
用普通 **Text** 代替，把 InventoryUI、InventorySlotUI 里的 `TextMeshProUGUI` 改成 `Text`，变量类型对应改一下。

**Q：格子显示错位？**  
检查 SlotGrid 的 Grid Layout Group 的 Cell Size、Constraint Count 是否按上面设置。

---

完成以上步骤后，背包 UI 就可以正常使用了。后续可以接入 UIManager 统一管理。
