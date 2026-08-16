# One-URL deployment

This project is prepared as one Docker web service. The React/Vite frontend and .NET API are hosted by the same ASP.NET application.

## Local test

```powershell
docker compose up --build
```

Open:
- http://localhost:8080
- http://localhost:8080/swagger
- http://localhost:8080/api/health

## Free cloud deployment with Render

1. Create a GitHub repository.
2. Upload the contents of this folder to the repository. The `Dockerfile` must be in the repository root.
3. Go to https://render.com and sign in.
4. Click **New +** -> **Web Service**.
5. Connect your GitHub account and select the repository.
6. Render should detect `Dockerfile`. If asked, choose **Docker**.
7. Choose the **Free** plan.
8. Add environment variables if you need AI/private GitHub features:
   - `OpenAI__ApiKey` = your OpenAI API key
   - `OpenAI__Model` = `gpt-4.1-mini`
   - `GitHub__Token` = a GitHub token (only needed for private repos or publishing)
9. Click **Create Web Service**.
10. Wait for the build to finish. Render will show a URL such as `https://ai-test-workflow-copilot-xxxx.onrender.com`.

The same URL serves the dashboard and API. No localhost URL is used by the production frontend.

## Important

- Do not commit real API keys into GitHub.
- The free Render service may sleep when idle; this does not depend on your laptop being on.
