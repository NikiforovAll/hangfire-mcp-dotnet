import { defineConfig } from 'vitepress'
import { withMermaid } from 'vitepress-plugin-mermaid'

// https://vitepress.dev/reference/site-config
export default withMermaid({
    title: "Nall.Hangfire.Mcp",
    description: "Remote MCP server for Hangfire — exposes background jobs as MCP tools.",
    base: '/hangfire-mcp-dotnet/',
    head: [
        ["link", { rel: "icon", type: "image/png", href: "/hangfire-mcp-dotnet/logo.png" }],
    ],
    themeConfig: {
        logo: '/logo.png',
        nav: [
            { text: 'Home', link: '/' },
            { text: 'Getting Started', link: '/getting-started' },
            { text: 'Configuration', link: '/configuration/sources' },
            { text: 'Authentication', link: '/authentication' },
            { text: 'Sample', link: '/samples/' },
        ],
        sidebar: {
            '/': [
                {
                    text: 'Introduction',
                    collapsed: false,
                    items: [
                        { text: 'What is Nall.Hangfire.Mcp?', link: '/introduction' },
                        { text: 'Getting Started', link: '/getting-started' },
                        { text: 'User Guide', link: '/user-guide' },
                    ]
                },
                {
                    text: 'Configuration',
                    collapsed: false,
                    items: [
                        { text: 'Discovery Sources', link: '/configuration/sources' },
                        { text: 'Options Reference', link: '/configuration/options' },
                        { text: 'Source Generator', link: '/configuration/source-generator' },
                    ]
                },
                {
                    text: 'Authentication',
                    collapsed: false,
                    items: [
                        { text: 'OAuth 2.1 / OIDC', link: '/authentication' },
                    ]
                },
                {
                    text: 'Sample',
                    collapsed: false,
                    items: [
                        { text: 'Overview', link: '/samples/' },
                        { text: 'Sample Jobs', link: '/samples/jobs' },
                        { text: 'MCP Inspector', link: '/samples/inspector' },
                        { text: 'Hangfire Dashboard', link: '/samples/dashboard' },
                    ]
                },
            ]
        },
        socialLinks: [
            { icon: 'github', link: 'https://github.com/NikiforovAll/hangfire-mcp-dotnet' }
        ],
        editLink: {
            pattern: 'https://github.com/NikiforovAll/hangfire-mcp-dotnet/edit/main/docs/:path'
        }
    },
    mermaid: {
        // https://mermaid.js.org/config/schema-docs/config.html
    }
});
