import { useMemo, useState } from 'react';
import './styles.css';

const API = import.meta.env.VITE_API_URL || '';

type AnyObj = Record<string, any>;

function pct(v: any) {
  const n = Number(v ?? 0);
  return `${(n * 100).toFixed(0)}%`;
}

function artifactUrl(value: string) {
  if (!value) return '#';
  return value.startsWith('http') ? value : `${API}${value}`;
}

export default function App() {
  const [url, setUrl] = useState('https://github.com/octocat/Hello-World');
  const [token, setToken] = useState('');
  const [result, setResult] = useState<AnyObj | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [active, setActive] = useState('overview');
  const [viewer, setViewer] = useState<{label:string,url:string,content:string,loading:boolean,error:string} | null>(null);

  async function analyze() {
    setBusy(true); setError(''); setResult(null); setActive('overview');
    try {
      const r = await fetch(`${API}/api/ai/analyze-and-generate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...(token ? { 'X-GitHub-Token': token } : {}) },
        body: JSON.stringify({ repositoryUrl: url, generateTests: true, generateWorkflow: true, generateCopilotInstructions: true })
      });
      const text = await r.text();
      let data: AnyObj;
      try { data = JSON.parse(text); } catch { throw new Error(text || `HTTP ${r.status}`); }
      if (!r.ok) throw new Error(data.error || text || `HTTP ${r.status}`);
      setResult(data);
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  }

  const repo = result?.repository ?? {};
  const predictions = result?.testPrediction?.predictions ?? [];
  const generated = result?.generatedTests?.tests ?? [];
  const artifacts = result?.artifacts ?? {};
  const high = predictions.filter((x: AnyObj) => String(x.priority).toLowerCase() === 'high').length;
  const languages = repo.languages ?? [];
  const frameworks = repo.frameworks ?? repo.testFrameworks ?? [];
  const files = repo.files ?? repo.testFiles ?? [];

  async function viewArtifact(label: string, value: string) {
    const fullUrl = artifactUrl(value);
    setViewer({ label, url: fullUrl, content: '', loading: true, error: '' });
    try {
      const r = await fetch(fullUrl);
      const text = await r.text();
      if (!r.ok) throw new Error(text || `HTTP ${r.status}`);
      setViewer({ label, url: fullUrl, content: text, loading: false, error: '' });
    } catch (e) {
      setViewer({ label, url: fullUrl, content: '', loading: false, error: e instanceof Error ? e.message : String(e) });
    }
  }

  const tabs = useMemo(() => [
    ['overview','Overview'], ['profile','Repository Profile'], ['prediction','AI Predictions'], ['tests','Generated Tests'], ['workflow','CI/CD Workflow'], ['copilot','Copilot'], ['logs','Logs & Artifacts']
  ], []);

  return <div className="app">
    <header className="topbar">
      <div className="brand"><div className="logo">AI</div><div><strong>Test Workflow Copilot</strong><span>Repository intelligence & test automation</span></div></div>
      <div className="status"><i className={busy ? 'busy' : result ? 'ok' : ''}></i>{busy ? 'Analyzing' : result ? 'Analysis complete' : 'Ready'}</div>
    </header>

    <div className="layout">
      <aside className="sidebar">
        <div className="side-title">WORKSPACE</div>
        {tabs.map(([id,label]) => <button key={id} className={active===id ? 'nav active' : 'nav'} onClick={() => setActive(id)}>{label}</button>)}
        <div className="side-note"><b>Execution</b><code>{result?.executionId ?? '—'}</code></div>
      </aside>

      <main className="content">
        <section className="hero">
          <div><div className="eyebrow">AI-POWERED REPOSITORY ANALYSIS</div><h1>Turn a GitHub repository into an intelligent test plan.</h1><p>Analyze public or private repositories, predict high-risk tests, generate test code, CI/CD workflow and GitHub Copilot instructions.</p></div>
          <div className="hero-badge">.NET 10<br/><span>+ Vite</span></div>
        </section>

        <section className="panel analyzer">
          <div className="panel-title"><div><h2>Repository Analyzer</h2><p>Enter a GitHub URL. A token is only needed for private repositories or GitHub Actions operations.</p></div></div>
          <div className="form-row"><input value={url} onChange={e=>setUrl(e.target.value)} placeholder="https://github.com/owner/repository"/><input value={token} onChange={e=>setToken(e.target.value)} placeholder="GitHub token (optional)" type="password"/><button className="primary" disabled={busy || !url} onClick={analyze}>{busy ? 'Analyzing…' : 'Analyze & Generate'}</button></div>
          {error && <div className="error"><b>Analysis failed</b><span>{error}</span></div>}
        </section>

        {!result && !busy && <section className="empty"><div className="empty-icon">⌁</div><h2>Dashboard ready</h2><p>Run an analysis to populate the repository profile, AI predictions, generated tests, workflow, Copilot instructions and execution logs.</p></section>}
        {busy && <section className="empty"><div className="spinner"></div><h2>Analyzing repository…</h2><p>Scanning repository structure and generating test intelligence.</p></section>}

        {result && active === 'overview' && <>
          <div className="cards">
            <Metric label="Files" value={repo.fileCount ?? files.length ?? 0}/><Metric label="Predicted tests" value={predictions.length}/><Metric label="High priority" value={high}/><Metric label="AI confidence" value={pct(result.testPrediction?.overallConfidence)}/>
          </div>
          <section className="grid2"><Panel title="Repository"><InfoRow k="Repository" v={`${repo.owner ?? ''}/${repo.name ?? ''}`}/><InfoRow k="Visibility" v={repo.visibility ?? 'Unknown'}/><InfoRow k="Description" v={repo.description || '—'}/><InfoRow k="Technologies" v={languages.join(', ') || '—'}/><InfoRow k="Frameworks" v={frameworks.join(', ') || '—'}/></Panel><Panel title="AI Test Prediction"><div className="confidence"><span>Overall confidence</span><b>{pct(result.testPrediction?.overallConfidence)}</b></div><div className="bar"><i style={{width:pct(result.testPrediction?.overallConfidence)}}></i></div><ul className="compact">{predictions.slice(0,5).map((x:AnyObj)=><li key={x.id ?? x.title}><span className={`pill ${String(x.priority).toLowerCase()}`}>{x.priority}</span><span>{x.title}</span><b>{pct(x.confidence)}</b></li>)}</ul></Panel></section>
          <section className="panel"><div className="panel-head"><h2>Generated Artifacts</h2><span>{result.executionId}</span></div><ArtifactGrid artifacts={artifacts} onView={viewArtifact}/></section>
        </>}

        {result && active === 'profile' && <section className="panel"><div className="panel-head"><h2>Repository Profile</h2><button className="link-button" onClick={() => viewArtifact('Repository profile', artifacts.profileUrl)}>View JSON</button></div><div className="cards mini"><Metric label="Files" value={repo.fileCount ?? 0}/><Metric label="Tests" value={repo.testFileCount ?? (repo.testFiles?.length ?? 0)}/><Metric label="Visibility" value={repo.visibility ?? '—'}/></div><div className="tags">{languages.map((x:string)=><span key={x}>{x}</span>)}{frameworks.map((x:string)=><span key={x}>{x}</span>)}</div><pre className="code">{JSON.stringify(repo,null,2)}</pre></section>}

        {result && active === 'prediction' && <section className="panel"><div className="panel-head"><h2>AI Test Predictions</h2><button className="link-button" onClick={() => viewArtifact('Predicted tests', artifacts.predictionUrl)}>View JSON</button></div><div className="prediction-list">{predictions.map((x:AnyObj)=><article className="prediction" key={x.id ?? x.title}><div><span className={`pill ${String(x.priority).toLowerCase()}`}>{x.priority}</span><h3>{x.title}</h3><p>{x.reason ?? x.description ?? 'AI-selected scenario based on repository evidence.'}</p></div><div className="score">{pct(x.confidence)}<small>confidence</small></div></article>)}</div></section>}

        {result && active === 'tests' && <section className="panel"><div className="panel-head"><h2>Generated Tests</h2><button className="link-button" onClick={() => viewArtifact('Generated tests', artifacts.generatedUrl)}>View JSON</button></div>{generated.map((t:AnyObj)=><article className="test-card" key={t.path}><div className="test-title"><h3>{t.path}</h3><button className="link-button" onClick={() => viewArtifact('Generated tests', artifacts.generatedUrl)}>View artifact</button></div><pre className="code">{t.code}</pre></article>)}</section>}

        {result && active === 'workflow' && <section className="panel"><div className="panel-head"><h2>AI Generated CI/CD Workflow</h2><button className="link-button" onClick={() => viewArtifact('CI workflow', artifacts.workflowUrl)}>View YAML</button></div><pre className="code large">{result.workflow}</pre></section>}

        {result && active === 'copilot' && <section className="panel"><div className="panel-head"><h2>GitHub Copilot Instructions</h2><button className="link-button" onClick={() => viewArtifact('Copilot instructions', artifacts.copilotUrl)}>View Markdown</button></div><pre className="code large">{result.copilotInstructions}</pre></section>}

        {result && active === 'logs' && <section className="panel"><div className="panel-head"><div><h2>Execution Logs & Artifacts</h2><p>Execution ID: <code>{result.executionId}</code></p></div></div><ArtifactGrid artifacts={artifacts} onView={viewArtifact}/><div className="log-link"><button className="secondary" onClick={() => viewArtifact('Execution log', artifacts.logUrl)}>View execution.log</button></div></section>}
      </main>
      {viewer && <div className="viewer-backdrop" onClick={() => setViewer(null)}>
        <section className="viewer" onClick={e => e.stopPropagation()}>
          <div className="viewer-head"><div><span>ARTIFACT VIEWER</span><h2>{viewer.label}</h2></div><button className="viewer-close" onClick={() => setViewer(null)}>×</button></div>
          <div className="viewer-url">{viewer.url}</div>
          {viewer.loading && <div className="viewer-loading">Loading artifact…</div>}
          {viewer.error && <div className="error"><b>Unable to load artifact</b><span>{viewer.error}</span></div>}
          {!viewer.loading && !viewer.error && <pre className="viewer-code">{viewer.content}</pre>}
        </section>
      </div>}
    </div>
  </div>
}

function Metric({label,value}:{label:string,value:any}) { return <div className="metric"><span>{label}</span><strong>{value}</strong></div> }
function Panel({title,children}:{title:string,children:any}) { return <div className="panel"><h2>{title}</h2>{children}</div> }
function InfoRow({k,v}:{k:string,v:any}) { return <div className="info"><span>{k}</span><b>{v}</b></div> }
function ArtifactGrid({artifacts,onView}:{artifacts:AnyObj,onView:(label:string,url:string)=>void}) { const items=[['profileUrl','Repository profile'],['predictionUrl','Predicted tests'],['generatedUrl','Generated tests'],['workflowUrl','CI workflow'],['copilotUrl','Copilot instructions'],['logUrl','Execution log']]; return <div className="artifact-grid">{items.map(([key,label])=>artifacts[key]&&<button className="artifact" key={key} onClick={() => onView(label, artifacts[key])}><span>{label}</span><small>{String(artifacts[key])}</small><b>View in dashboard →</b></button>)}</div> }
