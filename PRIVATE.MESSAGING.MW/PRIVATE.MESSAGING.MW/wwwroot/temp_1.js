
        async function showSessions(cId) {
            document.getElementById('sessionsModal').style.display = 'flex';
            const list = document.getElementById('sessionsList');
            list.innerHTML = 'Yükleniyor...';
            
            const c = clients[cId];
            try {
                const res = await fetch(`${API_BASE}/api/user/sessions`, {
                    headers: { 'Authorization': `Bearer ${c.token}` }
                });
                if (!res.ok) throw new Error("Oturumlar alınamadı");
                
                const sessions = await res.json();
                if (sessions.length === 0) {
                    list.innerHTML = 'Hiç cihaz kaydı bulunamadı.';
                    return;
                }
                
                list.innerHTML = '';

                [...sessions].reverse().forEach(s => {
                    const dt = new Date(s.lastActiveAt).toLocaleString();
                    list.innerHTML += `
                        <div style="background:#1e1e1e; padding:10px; border-radius:5px; margin-bottom:10px; border-left:3px solid #17a2b8;">
                            <strong>IP:</strong> ${s.ipAddress} <br/>
                            <strong>Cihaz:</strong> <span style="color:#aaa;">${s.deviceName}</span> <br/>
                            <strong>Son Görülme:</strong> ${dt}
                        </div>
                    `;
                });
            } catch(e) {
                list.innerHTML = 'Hata: ' + e.message;
            }
        }

        function updatePresenceUI(cId, isOnline, lastSeen) {
            const el = document.getElementById(`presence${cId}`);
            if (!el) return;
            
            if (isOnline) {
                el.innerHTML = '<span style="color:#28a745; font-weight:bold;">🟢 Çevrimiçi</span>';
            } else {
                const dt = lastSeen ? new Date(lastSeen).toLocaleString([], {hour: '2-digit', minute:'2-digit', day:'2-digit', month:'short'}) : 'Bilinmiyor';
                el.innerHTML = `Son Görülme: ${dt}`;
            }
        }
    