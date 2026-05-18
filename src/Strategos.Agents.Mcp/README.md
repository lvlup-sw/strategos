# LevelUp.Strategos.Agents.Mcp

Model Context Protocol adapter for `LevelUp.Strategos.Agents`. Provides the default `IMcpToolSource` implementation that wraps `ModelContextProtocol.Client` so Strategos agents can consume external MCP servers as skill providers.

This package is separate from `LevelUp.Strategos.Agents` by design: the core agents package stays free of MCP dependencies (port/adapter separation).

## Status

Preview — tracks the 2025-11-25 MCP specification.
