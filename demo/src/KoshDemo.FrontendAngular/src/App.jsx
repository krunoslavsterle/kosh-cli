import React, { useState, useEffect } from 'react'

export default function App() {
  const [apiData, setApiData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [apiError, setApiError] = useState(null)

  const fetchApiStatus = async () => {
    setLoading(true)
    setApiError(null)
    try {
      const apiUrl = window.location.hostname.includes('localhost') 
        ? `${window.location.protocol}//kosh-demo.api.localhost/api/status` 
        : 'http://localhost:6001/api/status'

      const res = await fetch(apiUrl).catch(() => fetch('http://localhost:6001/api/status'))
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const data = await res.json()
      setApiData(data)
    } catch (err) {
      setApiError(err.message)
    } finally {
      setLoading(false)
    }
  }

  const triggerApiError = async () => {
    try {
      await fetch('https://kosh-demo.api.localhost/api/simulate-error')
        .catch(() => fetch('http://localhost:6001/api/simulate-error'))
    } catch (e) {
      // Expected to fail
    }
    fetchApiStatus()
  }

  useEffect(() => {
    fetchApiStatus()
  }, [])

  return (
    <div className="container">
      <header>
        <span className="badge">🅰️ Angular Frontend App</span>
        <h1>Kosh CLI Demo — Angular Edition</h1>
        <p className="subtitle">Multi-Framework Microservice Orchestration</p>
      </header>

      <main>
        {/* Framework Navigation Bar */}
        <div style={{ display: 'flex', gap: '16px', marginBottom: '32px', justifyContent: 'center' }}>
          <a href="https://kosh-demo.react.localhost" style={{ padding: '12px 24px', background: 'rgba(56, 189, 248, 0.1)', border: '1px solid rgba(56, 189, 248, 0.3)', borderRadius: '12px', color: '#38bdf8', fontWeight: 600, textDecoration: 'none', transition: 'all 0.2s' }}>
            ← Switch to https://kosh-demo.react.localhost
          </a>
          <div style={{ padding: '12px 24px', background: 'rgba(244, 63, 94, 0.2)', border: '1px solid #f43f5e', borderRadius: '12px', color: '#f43f5e', fontWeight: 600 }}>
            🔒 https://kosh-demo.angular.localhost
          </div>
        </div>

        {/* API Connection Section */}
        <div className="card" style={{ marginBottom: '24px' }}>
          <div className="card-header">
            <span className="card-title">🌐 HTTPS API Connection (https://kosh-demo.api.localhost)</span>
            <span className={`status-dot ${apiError ? 'red' : 'green'}`}></span>
          </div>

          {loading ? (
            <p style={{ color: '#fda4af' }}>Querying https://kosh-demo.api.localhost/api/status...</p>
          ) : apiError ? (
            <p style={{ color: '#f87171' }}>Failed to connect: {apiError}. Is Kosh running?</p>
          ) : (
            <div>
              <p style={{ color: '#fff1f2', marginBottom: '12px' }}>
                Service: <code>{apiData?.service}</code> | Version: <code>{apiData?.version}</code> | Uptime: <code>{apiData?.uptime}</code>
              </p>
              <pre style={{ background: 'rgba(0,0,0,0.5)', padding: '12px', borderRadius: '8px', fontSize: '0.8rem', color: '#f43f5e', overflowX: 'auto' }}>
                {JSON.stringify(apiData, null, 2)}
              </pre>
            </div>
          )}

          <div style={{ marginTop: '16px', display: 'flex', gap: '12px' }}>
            <button onClick={fetchApiStatus} style={{ padding: '8px 16px', background: '#f43f5e', color: '#ffffff', border: 'none', borderRadius: '8px', fontWeight: 600, cursor: 'pointer' }}>
              🔄 Send Request to API
            </button>
            <button onClick={triggerApiError} style={{ padding: '8px 16px', background: 'rgba(248, 113, 113, 0.2)', color: '#f87171', border: '1px solid #f87171', borderRadius: '8px', fontWeight: 600, cursor: 'pointer' }}>
              ⚠️ Trigger API Error (Check Kosh TUI Log!)
            </button>
          </div>
        </div>

        {/* CLI Usage Info */}
        <div className="cli-usage">
          <h3>💡 Kosh CLI Command Cheat Sheet:</h3>
          <div className="cmd-line" style={{ marginBottom: '8px' }}>
            $ <code>kosh start -c demo/koshconfig.yaml</code>
          </div>
          <p style={{ fontSize: '0.85rem', color: '#fda4af' }}>
            Inside Kosh TUI: type <code>:v frontend-angular</code> for Angular logs, <code>:v api</code> for API logs!
          </p>
        </div>
      </main>
    </div>
  )
}
