const { spawn } = require('child_process');

const server = spawn('node', [
  'C:/Users/user/My project (7)/Library/PackageCache/com.gamelovers.mcp-unity@a32e47d4ec87/Server~/build/index.js'
]);

server.stdout.on('data', (data) => {
  const responses = data.toString().split('\n').filter(line => line.trim());
  for (const res of responses) {
    try {
      const parsed = JSON.parse(res);
      
      if (parsed.id === 1) {
        server.stdin.write(JSON.stringify({ jsonrpc: "2.0", method: "notifications/initialized" }) + '\n');
        
        server.stdin.write(JSON.stringify({
          jsonrpc: "2.0",
          id: 2,
          method: "tools/call",
          params: {
            name: "get_gameobject",
            arguments: {
              idOrName: "city_part_collider",
              maxDepth: 2,
              includeComponentProperties: true
            }
          }
        }) + '\n');
      } else if (parsed.id === 2) {
        require('fs').writeFileSync('C:/Users/user/My project (7)/city_colliders.json', JSON.stringify(parsed, null, 2));
        server.kill();
        process.exit(0);
      }
    } catch (e) {}
  }
});
server.stdin.write(JSON.stringify({ jsonrpc: "2.0", id: 1, method: "initialize", params: { protocolVersion: "2024-11-05", capabilities: {}, clientInfo: { name: "test", version: "1.0" } } }) + '\n');
