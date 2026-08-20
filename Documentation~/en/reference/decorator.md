# Decorator Nodes

`Decorator` is the single-child wrapper category for nodes that transform or observe a child's result. Decorators do not host services.

## Available nodes

- [`Always`](decorator/always/index.md) — return a configured result after executing one child.
- [`Capture`](decorator/capture/index.md) — store and forward a child's result.
- [`Inverter`](decorator/inverter/index.md) — invert a child's result.
- [`ResultChanged`](decorator/result-changed/index.md) — succeed when a child's result changes.
