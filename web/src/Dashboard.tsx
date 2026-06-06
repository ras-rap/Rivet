import { useState, useEffect } from 'react'

interface PlayerPos {
  id: number
  pos: { x: number, y: number, z: number }
  rot: { x: number, y: number, z: number }
  vel: { x: number, y: number, z: number }
}

interface PlayerInfo {
  id: number
  name: string
  ping: number
  steamId: string
}

export default function Dashboard({ apiKey, setApiKey }: { apiKey: string, setApiKey: (v: string) => void }) {
  const [playerPositions, setPlayerPositions] = useState<PlayerPos[]>([])
  const [players, setPlayers] = useState<PlayerInfo[]>([])
  const [message, setMessage] = useState('')
  const [isConnected, setIsConnected] = useState(false)
  
  const bounds = { minX: -5000, maxX: 5000, minZ: -5000, maxZ: 5000 }
  
  const host = window.location.host
  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
  const wsUrl = `${protocol}//${host}/api/ws?token=${apiKey}`

  const fetchPlayers = async () => {
    try {
      const res = await fetch('/api/players', {
        headers: { 'Authorization': `Bearer ${apiKey}` }
      })
      if (res.ok) setPlayers(await res.json())
    } catch (e) {}
  }

  useEffect(() => {
    if (!apiKey) return
    fetchPlayers()
    const pTimer = setInterval(fetchPlayers, 5000)
    const ws = new WebSocket(wsUrl)
    ws.onopen = () => setIsConnected(true)
    ws.onmessage = (e) => setPlayerPositions(JSON.parse(e.data))
    ws.onclose = () => setIsConnected(false)
    return () => {
      ws.close()
      clearInterval(pTimer)
    }
  }, [apiKey])

  const apiAction = async (path: string, body: any) => {
    try {
      await fetch(path, {
        method: 'POST',
        headers: { 
          'Authorization': `Bearer ${apiKey}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(body)
      })
      fetchPlayers()
    } catch (e) {
      alert("API request failed.")
    }
  }

  if (!apiKey) {
    return (
      <div className="max-w-md mx-auto mt-20 font-mono text-zinc-400">
        <div className="border border-zinc-800 bg-zinc-950 p-8 space-y-6 text-xs">
          <h2 className="text-white font-bold uppercase tracking-widest border-b border-zinc-800 pb-4">Auth Required</h2>
          <p>Please provide the X-RIVET-API-KEY to initialize the administrative console.</p>
          <input 
            id="api-key-input"
            type="password" 
            placeholder="API_KEY"
            className="w-full bg-zinc-900 border border-zinc-800 text-white rounded-none px-4 py-3 focus:outline-none focus:border-zinc-600 transition-colors"
            onKeyDown={(e) => {
              if (e.key === 'Enter') setApiKey((e.target as HTMLInputElement).value)
            }}
          />
          <button 
            onClick={() => {
              const input = document.getElementById('api-key-input') as HTMLInputElement
              setApiKey(input.value)
            }}
            className="w-full bg-zinc-800 hover:bg-zinc-700 text-white font-bold py-3 uppercase tracking-widest transition-colors border border-zinc-700"
          >
            Authorize
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="max-w-6xl mx-auto py-8 font-mono text-zinc-400 text-[10px]">
      <div className="flex justify-between items-center mb-8 border-b border-zinc-800 pb-4 uppercase font-bold">
        <div className="flex gap-6">
          <span className="text-white tracking-widest">Admin Console</span>
          <span className={isConnected ? 'text-emerald-500' : 'text-rose-500'}>
            [{isConnected ? 'Online' : 'Offline'}]
          </span>
        </div>
        <button 
          onClick={() => { setApiKey(''); localStorage.removeItem('rivet_api_key'); }}
          className="hover:text-white transition-colors"
        >
          [Exit Session]
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-8">
        <div className="lg:col-span-3 space-y-8">
          {/* Map */}
          <div className="bg-zinc-950 border border-zinc-800 relative aspect-video overflow-hidden">
             <div className="absolute inset-0 opacity-10" style={{ 
               backgroundImage: 'linear-gradient(#333 1px, transparent 1px), linear-gradient(90deg, #333 1px, transparent 1px)',
               backgroundSize: '40px 40px' 
             }} />
             
             {playerPositions.map(pp => {
               const name = players.find(p => p.id === pp.id)?.name || `UID_${pp.id}`
               const xPct = ((pp.pos.x - bounds.minX) / (bounds.maxX - bounds.minX)) * 100
               const zPct = ((pp.pos.z - bounds.minZ) / (bounds.maxZ - bounds.minZ)) * 100
               
               return (
                 <div 
                   key={pp.id}
                   className="absolute group"
                   style={{ left: `${xPct}%`, top: `${zPct}%`, transform: 'translate(-50%, -50%)' }}
                 >
                   <div 
                     className="w-2 h-2 bg-white border border-black shadow-[0_0_5px_white]"
                     style={{ transform: `rotate(${pp.rot.y}deg)` }}
                   />
                   <div className="absolute top-4 left-1/2 -translate-x-1/2 bg-black border border-zinc-800 px-1 py-0.5 text-[8px] text-white whitespace-nowrap opacity-0 group-hover:opacity-100 pointer-events-none z-10">
                     {name} [{Math.round(pp.pos.x)},{Math.round(pp.pos.z)}]
                   </div>
                 </div>
               )
             })}

             <div className="absolute bottom-2 left-2 text-[10px] text-zinc-600 uppercase font-bold tracking-widest">
               Grid Tracking: {playerPositions.length} Active
             </div>
          </div>

          {/* Broadcast */}
          <div className="bg-zinc-950 border border-zinc-800 p-6 space-y-4">
            <h3 className="text-white font-bold uppercase tracking-widest">Command Broadcast</h3>
            <div className="flex gap-2">
               <input 
                 className="flex-1 bg-zinc-900 border border-zinc-800 px-4 py-2 focus:outline-none focus:border-zinc-600 text-white"
                 placeholder="Enter message..."
                 value={message}
                 onChange={e => setMessage(e.target.value)}
                 onKeyDown={e => e.key === 'Enter' && apiAction('/api/say', { message }).then(() => setMessage(''))}
               />
               <button 
                 onClick={() => apiAction('/api/say', { message }).then(() => setMessage(''))}
                 className="bg-zinc-800 hover:bg-zinc-700 text-white px-6 py-2 border border-zinc-700 font-bold uppercase tracking-widest"
               >
                 Execute
               </button>
            </div>
          </div>
        </div>

        {/* List */}
        <div className="bg-zinc-950 border border-zinc-800 flex flex-col h-fit">
          <div className="bg-zinc-900 px-4 py-2 border-b border-zinc-800 text-white font-bold uppercase tracking-widest">
            Connected Nodes
          </div>
          <div className="divide-y divide-zinc-900 overflow-y-auto max-h-[600px]">
            {players.map(p => (
              <div key={p.id} className="p-4 space-y-3">
                <div className="flex justify-between items-start">
                  <div className="space-y-1">
                    <div className="text-white font-bold">[{p.id.toString().padStart(2, '0')}] {p.name}</div>
                    <div className="text-[8px] text-zinc-600">{p.steamId}</div>
                  </div>
                  <div className="text-zinc-500">{Math.round(p.ping)}ms</div>
                </div>
                <div className="flex gap-1">
                   <MiniActionBtn label="KICK" onClick={() => apiAction('/api/kick', { playerId: p.id })} />
                   <MiniActionBtn label="SLAY" onClick={() => apiAction('/api/slay', { playerId: p.id })} />
                   <MiniActionBtn label="BAN" onClick={() => apiAction('/api/ban', { playerId: p.id })} danger />
                </div>
              </div>
            ))}
            {players.length === 0 && (
              <div className="p-8 text-center text-zinc-600 italic">No nodes linked.</div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

function MiniActionBtn({ label, onClick, danger }: { label: string, onClick: () => void, danger?: boolean }) {
  return (
    <button 
      onClick={onClick}
      className={`flex-1 text-[8px] font-bold py-1 border transition-colors ${
        danger 
        ? 'border-rose-900/50 text-rose-800 hover:bg-rose-900/10 hover:text-rose-500' 
        : 'border-zinc-800 text-zinc-600 hover:text-white hover:border-zinc-700'
      }`}
    >
      [{label}]
    </button>
  )
}
