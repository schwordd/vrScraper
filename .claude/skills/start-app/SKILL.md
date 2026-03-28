---
name: start-app
description: Start the vrScraper app in background on port 5001
user-invocable: true
---

Start the vrScraper .NET app in the background:

```bash
cd D:/Source/vrScraper && dotnet run --project vrScraper --urls "http://localhost:5001" > /dev/null 2>&1 &
```

Wait 8 seconds for startup, then verify it's running:

```bash
sleep 8 && curl -s -o /dev/null -w "%{http_code}" http://localhost:5001/api/admin/test-normalize?title=test
```

If you get 200, the app is running. If not, check the process and logs.
