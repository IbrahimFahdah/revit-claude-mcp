import { defineConfig } from 'vitepress'
import { withMermaid } from 'vitepress-plugin-mermaid'

export default withMermaid(defineConfig({
  title: 'Revit–Claude AI Connector',
  description: 'Open source MCP bridge between Claude AI and Autodesk Revit',
  base: '/revit-claude-mcp/',
  themeConfig: {
    nav: [
      { text: 'Guide', link: '/guide/what-is-this' },
      { text: 'GitHub', link: 'https://github.com/IbrahimFahdah/revit-claude-mcp' }
    ],
    sidebar: [
      {
        text: 'Introduction',
        items: [
          { text: 'What is this?', link: '/guide/what-is-this' },
          { text: 'Installation', link: '/guide/installation' },
          { text: 'User Guide', link: '/guide/user-guide' },
          { text: 'Token Efficiency', link: '/guide/token-efficiency' }
        ]
      },
      {
        text: 'The MCP Server',
        items: [
          { text: 'How the Connector Server Works', link: '/guide/mcp-server' },
          { text: 'Testing with MCP Inspector', link: '/guide/mcp-inspector' },
          { text: 'Building the .mcpb Extension', link: '/guide/building-mcpb' }
        ]
      },
      {
        text: 'The Tool System',
        items: [
          { text: 'The Tool Registry', link: '/guide/tool-registry' },
          { text: 'Built-In Tools', link: '/guide/built-in-tools' },
          { text: 'Custom Tools', link: '/guide/custom-tools' },
          { text: 'Hot Reload', link: '/guide/hot-reload' }
        ]
      },
      {
        text: 'Community',
        items: [
          { text: 'Contributing', link: '/guide/contributing' }
        ]
      }
    ],
    socialLinks: [
      { icon: 'github', link: 'https://github.com/IbrahimFahdah/revit-claude-mcp' }
    ]
  }
}))
