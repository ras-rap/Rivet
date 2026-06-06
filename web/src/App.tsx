import { useState, useEffect } from 'react'
import Landing from './Landing'
import Dashboard from './Dashboard'

function App() {
  const [page, setPage] = useState<'landing' | 'dashboard'>('landing')
  const [apiKey, setApiKey] = useState(localStorage.getItem('rivet_api_key') || '')

  useEffect(() => {
    localStorage.setItem('rivet_api_key', apiKey)
  }, [apiKey])

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100 font-sans">
      <nav className="border-b border-zinc-800 bg-zinc-900/50 backdrop-blur-md sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-4 h-16 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 bg-indigo-600 rounded-lg flex items-center justify-center font-bold">R</div>
            <span className="font-bold text-xl tracking-tight">RIVET</span>
          </div>
          <div className="flex gap-4">
            <button 
              onClick={() => setPage('landing')}
              className={`px-3 py-2 rounded-md transition-colors ${page === 'landing' ? 'bg-zinc-800 text-white' : 'text-zinc-400 hover:text-white'}`}
            >
              Server Info
            </button>
            <button 
              onClick={() => setPage('dashboard')}
              className={`px-3 py-2 rounded-md transition-colors ${page === 'dashboard' ? 'bg-zinc-800 text-white' : 'text-zinc-400 hover:text-white'}`}
            >
              Admin Dashboard
            </button>
          </div>
        </div>
      </nav>

      <main className="max-w-7xl mx-auto p-4 md:p-8">
        {page === 'landing' ? (
          <Landing onGoToAdmin={() => setPage('dashboard')} />
        ) : (
          <Dashboard apiKey={apiKey} setApiKey={setApiKey} />
        )}
      </main>
    </div>
  )
}

export default App
