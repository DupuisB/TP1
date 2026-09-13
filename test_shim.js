const { spawn } = require('child_process');
const readline = require('readline');

const child = spawn('npx.cmd', ['-y', 'metavr', 'mcp', 'server'], {
  stdio: ['pipe', 'pipe', 'inherit'],
  shell: true
});

const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
  terminal: false
});

child.stdout.on('data', (data) => {
  process.stdout.write(data);
});

rl.on('line', (line) => {
  try {
    const msg = JSON.parse(line);
    if (msg.method === 'server/discover') {
      const resp = JSON.stringify({
        jsonrpc: '2.0',
        id: msg.id,
        error: { code: -32601, message: 'Method not found' }
      });
      process.stdout.write(resp + '\n');
      return;
    }
  } catch (e) {}
  child.stdin.write(line + '\n');
});

child.on('exit', (code) => {
  process.exit(code || 0);
});
