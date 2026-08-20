# 装饰器节点

`Decorator` 是单子节点包装节点类别，用于转换或观察子节点结果。Decorator 不承载 Service。

## 分类节点

- [`Always`](decorator/always/index.md)：执行一个子节点后返回配置结果。
- [`Capture`](decorator/capture/index.md)：保存并转发子节点结果。
- [`Inverter`](decorator/inverter/index.md)：反转子节点结果。
- [`ResultChanged`](decorator/result-changed/index.md)：子节点结果变化时成功。
