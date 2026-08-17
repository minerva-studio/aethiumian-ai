# 编辑器 API

### 编辑器区 `namespace Aethiumian.AI.Editor`

> 注意！位于这个namespace底下的所有脚本只允许在Editor中使用，意味着他们不可能在游戏编译完成后存在于游戏中

#### CustomNodeDrawerBase [code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/NodeDrawers/NodeDrawerBase.cs)

所有NodeDrawer的基类型，提供各类工具来绘制一个节点

#### DefaultDrawer [code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/NodeDrawers/DefaultNodeDrawer.cs)

默认节点绘制器

> 当一个节点没有设置一个绘制器时，该节点就会被默认绘制器所绘制

#### CustomNodeDrawerAttribute [code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/CustomNodeDrawerAttribute.cs)

自定义Node Drawer的Attribute，举例:

```c#
[CustomNodeDrawer(typeof(Always))]
public class AlwaysDrawer : CustomNodeDrawerBase
{
    ...
}
```

这是一个负责绘制节点Always的自定义绘制脚本

#### AIEditor [code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/AIEditorWindow/AIEditorWindow.cs)

AI Editor窗口的脚本
