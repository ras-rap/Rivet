import { useState, useEffect } from 'react'

interface Stats {
  players: number
  maxPlayers: number
  serverName: string
  mapId: number
  uptime: number
  hasPassword: boolean
}

interface Player {
  id: number
  name: string
  ping: number
}

export default function Landing({ onGoToAdmin }: { onGoToAdmin: () => void }) {
  const [stats, setStats] = useState<Stats | null>(null)
  const [players, setPlayers] = useState<Player[]>([])
  
  const fetchData = async () => {
    try {
      const statsRes = await fetch('/api/stats')
      const playersRes = await fetch('/api/players')
      if (statsRes.ok) setStats(await statsRes.json())
      if (playersRes.ok) setPlayers(await playersRes.json())
    } catch (e) {}
  }

  useEffect(() => {
    fetchData()
    const timer = setInterval(fetchData, 10000)
    return () => clearInterval(timer)
  }, [])

  return (
    <div className="max-w-4xl mx-auto py-12 font-mono text-zinc-300">
      <header className="border-b border-zinc-800 pb-8 mb-12">
        <h1 className="text-3xl font-bold text-white mb-2">rivet_server</h1>
        <p className="text-zinc-500 italic">A hobbyist reverse implementation of the Screw Drivers dedicated server.</p>
      </header>

      <section className="space-y-6 mb-16 text-sm leading-relaxed">
        <p>
          Rivet is a project I started because I was bored. It's a clean-room implementation of the networking protocol used by the game Screw Drivers.
        </p>
        <p>
          Unlike the original implementation, this server is designed for 24/7 uptime. It includes several improvements for long-running instances, such as better memory management and automated map rotation.
        </p>
        <div className="flex gap-4 pt-4">
          <button onClick={onGoToAdmin} className="bg-zinc-800 hover:bg-zinc-700 text-white px-4 py-2 border border-zinc-700">
            [Open Admin Console]
          </button>
        </div>
      </section>

      <section className="space-y-8">
        <h2 className="text-xl font-bold text-white uppercase tracking-widest border-l-4 border-indigo-600 pl-4">Current Telemetry</h2>
        
        <div className="grid grid-cols-1 md:grid-cols-2 gap-px bg-zinc-800 border border-zinc-800 overflow-hidden">
          <div className="bg-zinc-950 p-6">
            <span className="text-xs text-zinc-500 block mb-2 uppercase font-bold tracking-tighter">Instance Name</span>
            <span className="text-white">{stats?.serverName || 'Standalone Server'}</span>
          </div>
          <div className="bg-zinc-950 p-6">
            <span className="text-xs text-zinc-500 block mb-2 uppercase font-bold tracking-tighter">Uptime</span>
            <span className="text-white">{Math.floor((stats?.uptime || 0) / 60)} minutes</span>
          </div>
          <div className="bg-zinc-950 p-6">
            <span className="text-xs text-zinc-500 block mb-2 uppercase font-bold tracking-tighter">Active Map ID</span>
            <span className="text-white font-mono">{stats?.mapId || '—'}</span>
          </div>
          <div className="bg-zinc-950 p-6">
            <span className="text-xs text-zinc-500 block mb-2 uppercase font-bold tracking-tighter">Connected Clients</span>
            <span className="text-white">{stats?.players || 0} / {stats?.maxPlayers || 0}</span>
          </div>
        </div>

        <div className="bg-zinc-950 border border-zinc-800 overflow-hidden">
          <div className="bg-zinc-900 px-6 py-3 text-xs font-bold uppercase tracking-widest text-zinc-500 border-b border-zinc-800">
            Live Player List
          </div>
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-zinc-900 text-zinc-600">
                <th className="px-6 py-3 font-bold uppercase">UID</th>
                <th className="px-6 py-3 font-bold uppercase">Handle</th>
                <th className="px-6 py-3 font-bold uppercase text-right">Ping</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-zinc-900">
              {players.map(p => (
                <tr key={p.id} className="hover:bg-white/[0.02]">
                  <td className="px-6 py-3 text-zinc-500 font-mono">#{p.id.toString().padStart(2, '0')}</td>
                  <td className="px-6 py-3 text-white font-bold">{p.name}</td>
                  <td className="px-6 py-3 text-right font-mono">{Math.round(p.ping)}ms</td>
                </tr>
              ))}
              {players.length === 0 && (
                <tr>
                  <td colSpan={3} className="px-6 py-12 text-center text-zinc-600 italic">No active connections.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <footer className="mt-20 pt-8 border-t border-zinc-900 text-[10px] text-zinc-600 flex justify-between uppercase tracking-widest">
        <span>Rivet Server Core v0.1</span>
        <span>Standalone Implementation</span>
      </footer>
    </div>
  )
}


