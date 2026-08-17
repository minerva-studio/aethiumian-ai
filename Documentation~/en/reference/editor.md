# Editor API

### Editor area `namespace Aethiumian.AI.Editor`

> Attention! All scripts under this namespace are only allowed to be used in the Editor, which means that they cannot exist in the game after the game is compiled.

#### CustomNodeDrawerBase [code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/NodeDrawers/NodeDrawerBase.cs)

all NodeDrawers, providing various tools to draw a node

#### DefaultDrawer [code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/NodeDrawers/DefaultNodeDrawer.cs)

Default Node Painter

> When a node does not have a drawer set, the node is drawn by the default drawer

#### CustomNodeDrawerAttribute [code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/CustomNodeDrawerAttribute.cs)

Customize the Attribute of Node Drawer, for example :

````c#
[CustomNodeDrawer(typeof(Always))]
public class AlwaysDrawer : CustomNodeDrawerBase
{
...
}
````

custom draw script responsible for drawing the node Always

#### AIEditor [code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/AIEditorWindow/AIEditorWindow.cs)

AI Editor window
