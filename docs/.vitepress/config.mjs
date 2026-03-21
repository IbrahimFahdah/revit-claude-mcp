import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'Claude–Revit AI Connector',
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
          { text: 'Installation', link: '/guide/installation' }
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
          { text: 'The Tool Registry', link: '/guide/tool-registry' }
        ]
      }
    ],
    socialLinks: [
      { icon: 'github', link: 'https://github.com/IbrahimFahdah/revit-claude-mcp' }
    ]
  }
})
