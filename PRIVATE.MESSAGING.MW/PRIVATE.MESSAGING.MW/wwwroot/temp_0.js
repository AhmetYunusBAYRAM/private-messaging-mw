
        const API_BASE = window.location.origin.includes("localhost") && window.location.port !== "" ? window.location.origin : "http://localhost:5032";
        
        const clients = {
            1: { connection: null, rsa: new JSEncrypt(), token: '', nickname: '', email: '', blocked: [], messages: {}, activeReplyId: null, isRecording: false, mediaRecorder: null, audioChunks: [], pc: null, localStream: null, deviceId: null },
            2: { connection: null, rsa: new JSEncrypt(), token: '', nickname: '', email: '', blocked: [], messages: {}, activeReplyId: null, isRecording: false, mediaRecorder: null, audioChunks: [], pc: null, localStream: null, deviceId: null }
        };

        const iceServers = (window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1") 
            ? { iceServers: [] } 
            : { 
                iceServers: [
                    { urls: 'stun:stun.l.google.com:19302' },
                    { urls: 'stun:stun1.l.google.com:19302' },
                    { urls: 'turn:openrelay.metered.ca:80', username: 'openrelayproject', credential: 'openrelayproject' },
                    { urls: 'turn:openrelay.metered.ca:443', username: 'openrelayproject', credential: 'openrelayproject' },
                    { urls: 'turn:openrelay.metered.ca:443?transport=tcp', username: 'openrelayproject', credential: 'openrelayproject' }
                ] 
            };

        window.onload = () => {
            checkAutoLogin(1);
            checkAutoLogin(2);
        };

        function addSysLog(cId, msg, isError = false) {
            const logs = document.getElementById(`log${cId}`);
            logs.innerHTML += `<div class="${isError ? 'err-log' : 'sys-log'}">${msg}</div>`;
            logs.scrollTop = logs.scrollHeight;
        }


        let pressTimer;
        function startPress(e, cId, msgId) {
            cancelPress();
            pressTimer = setTimeout(() => {
                showReactionMenu(cId, msgId);
            }, 400); 
        }

        function cancelPress() {
            if (pressTimer) clearTimeout(pressTimer);
        }

        function showReactionMenu(cId, msgId) {
            document.querySelectorAll('.reaction-menu').forEach(m => m.style.display = 'none');
            const menu = document.getElementById(`rxMenu_${cId}_${msgId}`);
            if (menu) menu.style.display = 'flex';
        }

        document.addEventListener('mousedown', (e) => {
            if (!e.target.closest('.msg-bubble') && !e.target.closest('.reaction-menu')) {
                document.querySelectorAll('.reaction-menu').forEach(m => m.style.display = 'none');
            }
        });
        document.addEventListener('touchstart', (e) => {
            if (!e.target.closest('.msg-bubble') && !e.target.closest('.reaction-menu')) {
                document.querySelectorAll('.reaction-menu').forEach(m => m.style.display = 'none');
            }
        });


        let startX = 0;
        let isDragging = false;
        let currentContainer = null;

        function handleDragStart(e) {
            if (e.target.closest('.reaction-menu')) return;
            isDragging = true;
            startX = e.type.includes('mouse') ? e.clientX : e.touches[0].clientX;
            currentContainer = e.currentTarget;
            currentContainer.style.transition = 'none';
        }

        function handleDragMove(e) {
            if (!isDragging || !currentContainer) return;
            let currentX = e.type.includes('mouse') ? e.clientX : e.touches[0].clientX;
            let diffX = currentX - startX;
            
            if (Math.abs(diffX) > 10) cancelPress();

            if (diffX > 0 && diffX < 80) {
                currentContainer.style.transform = `translateX(${diffX}px)`;
                const hint = currentContainer.querySelector('.reply-hint');
                if (hint) hint.style.opacity = diffX / 50;
            }
        }

        function handleDragEnd(e, cId, msgId) {
            if (!isDragging || !currentContainer) return;
            isDragging = false;
            
            let endX = e.type.includes('mouse') ? e.clientX : (e.changedTouches ? e.changedTouches[0].clientX : startX);
            let diffX = endX - startX;

            currentContainer.style.transition = 'transform 0.2s';
            currentContainer.style.transform = `translateX(0px)`;
            const hint = currentContainer.querySelector('.reply-hint');
            if (hint) hint.style.opacity = 0;
            
            currentContainer = null;

            if (diffX > 40) {
                setReply(cId, msgId);
            }
        }

        function setReply(cId, msgId) {
            const c = clients[cId];
            if (!c.messages[msgId]) return;
            c.activeReplyId = msgId;
            const previewBox = document.getElementById(`replyPreview${cId}`);
            const previewText = document.getElementById(`replyText${cId}`);
            let txt = c.messages[msgId];
            
            if (txt.startsWith('[IMAGE]')) {
                previewText.innerText = "📷 Fotoğraf";
            } else if (txt.startsWith('[AUDIO]')) {
                previewText.innerText = "🎤 Sesli Mesaj";
            } else if (txt.startsWith('[DELETED]')) {
                previewText.innerText = "🚫 Silinmiş mesaj";
            } else {
                previewText.innerText = txt.length > 50 ? txt.substring(0, 50) + "..." : txt;
            }
            
            previewBox.style.display = 'block';
            document.getElementById(`msg${cId}`).focus();
        }

        function cancelReply(cId) {
            clients[cId].activeReplyId = null;
            document.getElementById(`replyPreview${cId}`).style.display = 'none';
        }

        function enlargeImage(src) {
            document.getElementById('enlargedImg').src = src;
            document.getElementById('imageModal').style.display = 'flex';
        }


        function renderMessage(cId, msgId, sender, text, timestamp, reactions = {}, replyToId = null, isDeleted = false, isRead = false) {
            const logs = document.getElementById(`log${cId}`);
            const c = clients[cId];
            const isMe = sender === c.nickname;
            const timeStr = new Date(timestamp).toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'});
            
            c.messages[msgId] = isDeleted ? "[DELETED]" : text;

            let rxHtml = '';
            if (!isDeleted) {
                const emojiCounts = {};
                let totalRx = 0;
                for (const [nick, emoji] of Object.entries(reactions)) {
                    emojiCounts[emoji] = (emojiCounts[emoji] || 0) + 1;
                    totalRx++;
                }
                if (totalRx > 0) {
                    let displayEmojis = Object.keys(emojiCounts).join(' ');
                    rxHtml = `<div class="reaction-badge" title="Tepkiler">${displayEmojis} ${totalRx > 1 ? totalRx : ''}</div>`;
                }
            }

            let replyHtml = '';
            if (replyToId && !isDeleted) {
                let repliedText = c.messages[replyToId];
                if (!repliedText) repliedText = "Şifreli mesaj (Eski mesaj).";
                
                if (repliedText.startsWith('[IMAGE]')) {
                    repliedText = "📷 Fotoğraf";
                } else if (repliedText.startsWith('[AUDIO]')) {
                    repliedText = "🎤 Sesli Mesaj";
                } else if (repliedText.startsWith('[DELETED]')) {
                    repliedText = "🚫 Silinmiş mesaj";
                } else if (repliedText.length > 60) {
                    repliedText = repliedText.substring(0, 60) + "...";
                }
                replyHtml = `<div class="reply-quote" onclick="document.getElementById('msgBox_${cId}_${replyToId}')?.scrollIntoView({behavior:'smooth'})">${repliedText}</div>`;
            }

            let renderedText = '';
            if (isDeleted) {
                renderedText = `<div style="color:#aaa; font-style:italic; font-size:13px;">🚫 Bu mesaj gönderen tarafından silindi.</div>`;
            } else if (text.startsWith('[IMAGE]')) {
                const base64Str = text.replace('[IMAGE]', '');
                renderedText = `<img src="${base64Str}" style="max-width:200px; border-radius:8px; cursor:pointer;" onclick="enlargeImage(this.src)">`;
            } else if (text.startsWith('[AUDIO]')) {
                const base64Str = text.replace('[AUDIO]', '');
                renderedText = `<audio controls src="${base64Str}" style="max-width: 250px; height: 40px; outline: none;"></audio>`;
            } else {
                renderedText = `<div>${text.replace(/</g, "&lt;").replace(/>/g, "&gt;")}</div>`;
            }

            let tickHtml = '';
            if (isMe && !isDeleted) {
                tickHtml = isRead ? '<span style="color:#4db8ff; font-weight:bold;">✓✓</span>' : '<span style="color:#aaa;">✓</span>';
            }

            const html = `
                <div class="msg-container ${isMe ? 'msg-out' : 'msg-in'}" id="msgBox_${cId}_${msgId}"
                     ondblclick="!${isDeleted} && setReply('${cId}', '${msgId}')"
                     onmousedown="!${isDeleted} && handleDragStart(event)"
                     onmousemove="!${isDeleted} && handleDragMove(event)"
                     onmouseup="!${isDeleted} && handleDragEnd(event, '${cId}', '${msgId}')"
                     onmouseleave="!${isDeleted} && handleDragEnd(event, '${cId}', '${msgId}')"
                     ontouchstart="!${isDeleted} && handleDragStart(event)"
                     ontouchmove="!${isDeleted} && handleDragMove(event)"
                     ontouchend="!${isDeleted} && handleDragEnd(event, '${cId}', '${msgId}')"
                     ontouchcancel="!${isDeleted} && handleDragEnd(event, '${cId}', '${msgId}')">
                    <div class="reply-hint">↩️</div>
                    <div class="msg-bubble" 
                         onmousedown="startPress(event, '${cId}', '${msgId}')" 
                         onmouseup="cancelPress()" 
                         onmouseleave="cancelPress()" 
                         ontouchstart="startPress(event, '${cId}', '${msgId}')" 
                         ontouchend="cancelPress()" 
                         ontouchcancel="cancelPress()">
                        ${replyHtml}
                        ${renderedText}
                        <span class="msg-time">${timeStr} <span id="tick_${cId}_${msgId}">${tickHtml}</span></span>
                        ${rxHtml}
                        ${!isDeleted ? `
                        <div class="reaction-menu" id="rxMenu_${cId}_${msgId}">
                            <span onclick="event.stopPropagation(); sendReaction(${cId}, '${msgId}', '👍')">👍</span>
                            <span onclick="event.stopPropagation(); sendReaction(${cId}, '${msgId}', '❤️')">❤️</span>
                            <span onclick="event.stopPropagation(); sendReaction(${cId}, '${msgId}', '😂')">😂</span>
                            <span onclick="event.stopPropagation(); sendReaction(${cId}, '${msgId}', '😮')">😮</span>
                            <span onclick="event.stopPropagation(); sendReaction(${cId}, '${msgId}', '😢')">😢</span>
                            ${isMe ? `<span style="font-size:14px; margin-left:10px; color:#ff4444; border-left:1px solid #444; padding-left:10px;" onclick="event.stopPropagation(); deleteMessage(${cId}, '${msgId}')">🗑️ Sil</span>` : ''}
                        </div>` : ''}
                    </div>
                </div>
            `;
            logs.innerHTML += html;
            logs.scrollTop = logs.scrollHeight;
        }

        async function sendReaction(cId, msgId, emoji) {
            document.querySelectorAll('.reaction-menu').forEach(m => m.style.display = 'none');
            const c = clients[cId];
            if (!c.connection) return;
            
            const to = document.getElementById(`to${cId}`).value;
            if (c.blocked.includes(to)) {
                return alert("Bu kullanıcıyı engellediğiniz için ifade bırakamazsınız.");
            }
            
            try {
                await c.connection.invoke("AddReaction", msgId, emoji);
            } catch (err) {
                alert(err.message);
                addSysLog(cId, `❌ İfade Hatası: ${err.message}`, true);
            }
        }

        async function deleteMessage(cId, msgId) {
            document.querySelectorAll('.reaction-menu').forEach(m => m.style.display = 'none');
            const c = clients[cId];
            if (!c.connection) return;

            if (confirm("Bu mesajı herkes için silmek istediğinize emin misiniz? (Geri alınamaz)")) {
                try {
                    await c.connection.invoke("DeleteMessage", msgId);
                } catch (err) {
                    alert(err.message);
                }
            }
        }

        const pubKeyCache = {};

        async function getPublicKey(cId, to) {
            if (pubKeyCache[to]) return pubKeyCache[to];
            const c = clients[cId];
            const res = await fetch(`${API_BASE}/api/auth/publickey/${to}`, {
                headers: { 'Authorization': `Bearer ${c.token}` }
            });
            if (!res.ok) return null;
            const data = await res.json();
            pubKeyCache[to] = data.publicKey;
            return data.publicKey;
        }


        async function sendDirectE2EEMessage(cId, to, payloadText) {
            const c = clients[cId];
            const targetPublicKey = await getPublicKey(cId, to);
            if (!targetPublicKey) return console.error("Could not fetch target public key");

            const aesKey = CryptoJS.lib.WordArray.random(16).toString();
            const encryptedPayload = CryptoJS.AES.encrypt(payloadText, aesKey).toString();

            const encryptorReceiver = new JSEncrypt();
            encryptorReceiver.setPublicKey(targetPublicKey);
            const receiverSymKey = encryptorReceiver.encrypt(aesKey);

            const myPublicKey = c.rsa.getPublicKey();
            const encryptorSelf = new JSEncrypt();
            encryptorSelf.setPublicKey(myPublicKey);
            const senderSymKey = encryptorSelf.encrypt(aesKey);

            await c.connection.invoke("SendPrivateMessage", to, senderSymKey, receiverSymKey, encryptedPayload, null);
        }


        async function refreshJwtToken(cId) {
            const c = clients[cId];
            if (!c.token || !c.refreshToken) return;
            
            try {
                const res = await fetch(`${API_BASE}/api/auth/refresh-token`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ expiredToken: c.token, refreshToken: c.refreshToken })
                });
                
                if (res.ok) {
                    const data = await res.json();
                    c.token = data.token;
                    c.refreshToken = data.refreshToken;
                } else {
                    console.log("Refresh token expired or invalid. User needs to login again.");
                }
            } catch(e) {
                console.error("Error refreshing token:", e);
            }
        }

        async function initPeerConnection(cId, targetNickname) {
            const c = clients[cId];
            if (c.pc) c.pc.close();
            c.pc = new RTCPeerConnection(iceServers);
            c.iceQueue = c.iceQueue || [];

            c.pc.onicecandidate = async e => {
                if (e.candidate) {
                    addSysLog(cId, `📤 ICE adresi gönderiliyor`);
                    const payload = `[WEBRTC_ICE]${JSON.stringify(e.candidate)}`;
                    await sendDirectE2EEMessage(cId, targetNickname, payload);
                }
            };

            c.pc.onconnectionstatechange = e => {
                addSysLog(cId, `⚡ WebRTC Durumu: ${c.pc.connectionState}`);
                if (c.pc.connectionState === 'connected') {
                    addSysLog(cId, `🟢 Bağlantı Kuruldu! (Ses Aktarılıyor)`);
                    const remoteAudio = document.getElementById(`remoteAudio${cId}`);
                    if (remoteAudio) {
                        remoteAudio.play().catch(err => console.error("Force play hatası:", err));
                    }
                }
            };
            c.pc.oniceconnectionstatechange = e => {
                addSysLog(cId, `🧊 ICE Durumu: ${c.pc.iceConnectionState}`);
            };

            c.pc.ontrack = e => {
                addSysLog(cId, `🎵 Ses paketi alındı (Track)`);
                const remoteAudio = document.getElementById(`remoteAudio${cId}`);
                if (remoteAudio) {
                    remoteAudio.srcObject = (e.streams && e.streams[0]) ? e.streams[0] : new MediaStream([e.track]);
                    remoteAudio.play().catch(err => {
                        console.error("Ses oynatılamadı:", err);
                        addSysLog(cId, `❌ Ses oynatılamadı: ${err.message}`, true);
                    });
                }
            };

            try {
                c.localStream = await navigator.mediaDevices.getUserMedia({ audio: true });
                c.localStream.getTracks().forEach(track => c.pc.addTrack(track, c.localStream));
            } catch (err) {
                alert("Mikrofon izni reddedildi!");
                throw err;
            }
        }

        async function startCall(cId) {
            const to = document.getElementById(`to${cId}`).value;
            if (!to) return alert("Arama yapmak için önce sohbet seçin.");
            const c = clients[cId];

            if (c.blocked.includes(to)) return alert("Engellediğiniz kişiyi arayamazsınız.");

            c.iceQueue = [];
            showCallModal(cId, `Aranıyor: ${to}...`);
            await initPeerConnection(cId, to);

            const offer = await c.pc.createOffer();
            await c.pc.setLocalDescription(offer);
            
            const payload = `[WEBRTC_OFFER]${JSON.stringify({ type: offer.type, sdp: offer.sdp })}`;
            await sendDirectE2EEMessage(cId, to, payload);
            addSysLog(cId, `📞 ${to} aranıyor...`);
        }

        async function processIceQueue(cId) {
            const c = clients[cId];
            if (c.iceQueue && c.iceQueue.length > 0 && c.pc && c.pc.remoteDescription) {
                addSysLog(cId, `🔄 ${c.iceQueue.length} adet bekleyen ICE işleniyor...`);
                for (let cand of c.iceQueue) {
                    try { 
                        await c.pc.addIceCandidate(new RTCIceCandidate(cand)); 
                        addSysLog(cId, `✅ Kuyruktan ICE Eklendi`);
                    } catch(e){}
                }
                c.iceQueue = [];
            }
        }

        async function handleWebRTC(cId, from, payload) {
            const c = clients[cId];
            c.iceQueue = c.iceQueue || [];

            if (payload.startsWith('[WEBRTC_OFFER]')) {
                const offerObj = JSON.parse(payload.replace('[WEBRTC_OFFER]', ''));
                c.pendingCall = { from, offer: offerObj };
                document.getElementById(`incomingCallText${cId}`).innerText = `📞 ${from} sesli arıyor...`;
                document.getElementById(`incomingCallModal${cId}`).style.display = 'block';
                addSysLog(cId, `📞 ${from} arıyor...`);
            } 
            else if (payload.startsWith('[WEBRTC_ANSWER]')) {
                const answerObj = JSON.parse(payload.replace('[WEBRTC_ANSWER]', ''));
                if (c.pc) {
                    await c.pc.setRemoteDescription(new RTCSessionDescription(answerObj));
                    document.getElementById(`callModalText${cId}`).innerText = `Konuşuluyor: ${from}`;
                    await processIceQueue(cId);
                }
            } 
            else if (payload.startsWith('[WEBRTC_ICE]')) {
                const candidateObj = JSON.parse(payload.replace('[WEBRTC_ICE]', ''));
                if (c.pc && c.pc.remoteDescription) {
                    try { 
                        await c.pc.addIceCandidate(new RTCIceCandidate(candidateObj)); 
                        addSysLog(cId, `✅ ICE Eklendi`);
                    } catch(e){
                        addSysLog(cId, `❌ ICE Hatası: ${e.message}`, true);
                    }
                } else {
                    addSysLog(cId, `⏳ ICE Bekletiliyor`);
                    c.iceQueue.push(candidateObj);
                }
            }
        }

        async function answerCall(cId) {
            const c = clients[cId];
            document.getElementById(`incomingCallModal${cId}`).style.display = 'none';
            if (!c.pendingCall) return;

            const from = c.pendingCall.from;
            const offerObj = c.pendingCall.offer;
            
            showCallModal(cId, `Konuşuluyor: ${from}`);
            await initPeerConnection(cId, from);
            
            await c.pc.setRemoteDescription(new RTCSessionDescription(offerObj));
            const answer = await c.pc.createAnswer();
            await c.pc.setLocalDescription(answer);

            const payloadAns = `[WEBRTC_ANSWER]${JSON.stringify({ type: answer.type, sdp: answer.sdp })}`;
            await sendDirectE2EEMessage(cId, from, payloadAns);
            
            await processIceQueue(cId);
            addSysLog(cId, `📞 ${from} ile çağrı başladı.`);
            c.pendingCall = null;
        }

        function rejectCall(cId) {
            document.getElementById(`incomingCallModal${cId}`).style.display = 'none';
            clients[cId].pendingCall = null;
            clients[cId].iceQueue = [];
        }

        function toggleMute(cId) {
            const c = clients[cId];
            if (c.localStream) {
                const audioTrack = c.localStream.getAudioTracks()[0];
                if (audioTrack) {
                    audioTrack.enabled = !audioTrack.enabled;
                    const btn = document.getElementById(`muteBtn${cId}`);
                    btn.innerText = audioTrack.enabled ? "🔇 Sustur" : "🔊 Sesi Aç";
                    btn.style.background = audioTrack.enabled ? "#ffc107" : "#28a745";
                }
            }
        }

        function showCallModal(cId, text) {
            const modal = document.getElementById(`callModal${cId}`);
            document.getElementById(`callModalText${cId}`).innerText = text;
            modal.style.display = 'block';
            const btn = document.getElementById(`muteBtn${cId}`);
            if (btn) {
                btn.innerText = "🔇 Sustur";
                btn.style.background = "#ffc107";
            }
        }

        function endCall(cId) {
            const c = clients[cId];
            if (c.pc) { c.pc.close(); c.pc = null; }
            if (c.localStream) { c.localStream.getTracks().forEach(t => t.stop()); c.localStream = null; }
            document.getElementById(`callModal${cId}`).style.display = 'none';
            addSysLog(cId, `📞 Çağrı sonlandırıldı.`);
        }


        async function processAndSendMessage(cId, payloadText) {
            const c = clients[cId];
            const to = document.getElementById(`to${cId}`).value;
            if (!to) return;

            if (c.blocked.includes(to)) {
                return alert("Bu kullanıcıyı engellediniz. Mesaj göndermek için engeli kaldırın.");
            }

            const targetStaticKey = await getPublicKey(cId, to);
            if (!targetStaticKey) return alert(`Alıcı (${to}) bulunamadı!`);

            let activeDevices = {};
            if (c.connection && c.connection.state === signalR.HubConnectionState.Connected) {
                activeDevices = await c.connection.invoke("GetEphemeralPublicKeys", to);
            }

            const aesKey = CryptoJS.lib.WordArray.random(16).toString();
            const encryptedPayload = CryptoJS.AES.encrypt(payloadText, aesKey).toString();

            const ephemeralSymKeys = {};
            
            const encryptorStatic = new JSEncrypt();
            encryptorStatic.setPublicKey(targetStaticKey);
            ephemeralSymKeys["STATIC"] = encryptorStatic.encrypt(aesKey);

            if (activeDevices) {
                for (const [deviceId, ephemeralKey] of Object.entries(activeDevices)) {
                    const encryptorEphemeral = new JSEncrypt();
                    encryptorEphemeral.setPublicKey(ephemeralKey);
                    ephemeralSymKeys[deviceId] = encryptorEphemeral.encrypt(aesKey);
                }
            }

            const myPublicKey = c.rsa.getPublicKey();
            const encryptorSelf = new JSEncrypt();
            encryptorSelf.setPublicKey(myPublicKey);
            const senderSymKey = encryptorSelf.encrypt(aesKey);

            const sig = new JSEncrypt();
            sig.setPrivateKey(c.rsa.getPrivateKey());
            const signature = sig.sign(payloadText, CryptoJS.SHA256, "sha256");

            try {
                const replyToId = c.activeReplyId;
                const msgId = await c.connection.invoke("SendPrivateMessage", to, senderSymKey, ephemeralSymKeys, signature, encryptedPayload, replyToId);
                renderMessage(cId, msgId, c.nickname, payloadText, new Date().toISOString(), {}, replyToId, false);
                
                cancelReply(cId);
                await loadInbox(cId);
            } catch (err) {
                addSysLog(cId, `❌ HATA: ${err.message}`, true);
                alert(err.message);
            }
        }

        async function sendMessage(cId) {
            const msgInput = document.getElementById(`msg${cId}`);
            const msg = msgInput.value.trim();
            if (!msg) return;
            await processAndSendMessage(cId, msg);
            msgInput.value = "";
        }

        async function sendImage(cId, input) {
            const file = input.files[0];
            if (!file) return;
            
            const reader = new FileReader();
            reader.onload = async function(e) {
                if (file.type === 'image/gif') {

                    const dataUrl = e.target.result;
                    const finalPayload = `[IMAGE]${dataUrl}`;
                    input.value = "";
                    await processAndSendMessage(cId, finalPayload);
                    return;
                }
                const img = new Image();
                img.onload = async function() {
                    const canvas = document.createElement('canvas');
                    let width = img.width;
                    let height = img.height;
                    const MAX_SIZE = 1600;
                    
                    if (width > height) {
                        if (width > MAX_SIZE) { height *= MAX_SIZE / width; width = MAX_SIZE; }
                    } else {
                        if (height > MAX_SIZE) { width *= MAX_SIZE / height; height = MAX_SIZE; }
                    }
                    
                    canvas.width = width;
                    canvas.height = height;
                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0, width, height);
                    
                    const dataUrl = canvas.toDataURL('image/jpeg', 0.9);
                    const finalPayload = `[IMAGE]${dataUrl}`;
                    
                    await processAndSendMessage(cId, finalPayload);
                    input.value = ""; 
                }
                img.src = e.target.result;
            }
            reader.readAsDataURL(file);
        }

        async function toggleRecording(cId) {
            const c = clients[cId];
            const btn = document.getElementById(`micBtn${cId}`);
            
            if (!c.isRecording) {
                try {
                    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                    c.mediaRecorder = new MediaRecorder(stream);
                    c.audioChunks = [];
                    
                    c.mediaRecorder.ondataavailable = e => {
                        if (e.data.size > 0) c.audioChunks.push(e.data);
                    };
                    
                    c.mediaRecorder.onstop = async () => {
                        const audioBlob = new Blob(c.audioChunks, { type: 'audio/webm' });
                        const reader = new FileReader();
                        reader.onload = async function() {
                            const base64Data = reader.result;
                            const finalPayload = `[AUDIO]${base64Data}`;
                            await processAndSendMessage(cId, finalPayload);
                        };
                        reader.readAsDataURL(audioBlob);
                        
                        stream.getTracks().forEach(track => track.stop());
                    };
                    
                    c.mediaRecorder.start();
                    c.isRecording = true;
                    btn.innerText = "⏹️";
                    btn.style.background = "#dc3545"; 
                } catch (err) {
                    alert("Mikrofon izni alınamadı: " + err.message);
                }
            } else {
                if (c.mediaRecorder && c.mediaRecorder.state !== 'inactive') {
                    c.mediaRecorder.stop();
                }
                c.isRecording = false;
                btn.innerText = "🎙️";
                btn.style.background = "#17a2b8";
            }
        }


        async function checkAutoLogin(cId) {
            let deviceId = localStorage.getItem(`deviceId${cId}`);
            if (!deviceId) {
                deviceId = crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(36).substring(2, 15);
                localStorage.setItem(`deviceId${cId}`, deviceId);
            }
            clients[cId].deviceId = deviceId;

            const token = localStorage.getItem(`token${cId}`);
            const nickname = localStorage.getItem(`nickname${cId}`);
            const email = localStorage.getItem(`email${cId}`);
            const privateKey = localStorage.getItem(`rsa_private_${email}`);

            if (token && nickname && email && privateKey) {
                const c = clients[cId];
                c.token = token;
                c.nickname = nickname;
                c.email = email;
                c.rsa.setPrivateKey(privateKey);
                
                document.getElementById(`auth${cId}`).style.display = 'none';
                document.getElementById(`app${cId}`).style.display = 'block';
                document.getElementById(`myName${cId}`).innerText = c.nickname;

                await fetchProfile(cId);
                await loadBlockedListUI(cId);
                await loadInbox(cId);
                await startSignalR(cId);
            }
        }

        function logout(cId) {
            localStorage.removeItem(`token${cId}`);
            localStorage.removeItem(`nickname${cId}`);
            localStorage.removeItem(`email${cId}`);
            if(clients[cId].connection) clients[cId].connection.stop();
            
            document.getElementById(`auth${cId}`).style.display = 'block';
            document.getElementById(`app${cId}`).style.display = 'none';
            document.getElementById(`log${cId}`).innerHTML = '';
        }

        async function requestOtp(cId, action) {
            const email = document.getElementById(`email${cId}`).value;
            const nickname = document.getElementById(`nick${cId}`).value;
            clients[cId].email = email;

            let endpoint = action === 'register' ? '/api/auth/register' : '/api/auth/login';
            let body = {};

            if (action === 'register') {
                const pin = prompt("Eski mesajlarınızı cihaz değiştirince kurtarabilmek için bir Kurtarma Parolası (PIN) belirleyin:");
                if (!pin) return alert("Parola zorunludur!");
                
                alert("Güvenliğiniz için uçtan uca şifreleme anahtarları oluşturuluyor. Lütfen 3-5 saniye bekleyin...");

                setTimeout(async () => {
                    try {
                        const c = clients[cId];
                        c.rsa.getKey();
                        const publicKey = c.rsa.getPublicKey();
                        const privateKey = c.rsa.getPrivateKey();
                        
                        const encryptedPrivateKey = CryptoJS.AES.encrypt(privateKey, pin).toString();
                        localStorage.setItem(`rsa_private_${email}`, privateKey);
                        
                        body = { email, nickname, publicKey, encryptedPrivateKey };

                        const res = await fetch(`${API_BASE}${endpoint}`, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify(body)
                        });

                        if (res.ok) alert(`OTP gönderildi (Terminal ekranına bakınız).`);
                        else { const data = await res.json(); alert(`Hata: ${data.message}`); }
                    } catch (e) {
                        alert("Kayıt sırasında bir hata oluştu: " + e.message);
                    }
                }, 100);
                return;
            } else {
                body = { email };
            }

            const res = await fetch(`${API_BASE}${endpoint}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });

            if (res.ok) alert(`OTP gönderildi (Terminal ekranına bakınız).`);
            else { const data = await res.json(); alert(`Hata: ${data.message}`); }
        }

        async function verifyOtp(cId) {
            const email = clients[cId].email;
            const otp = document.getElementById(`otp${cId}`).value;
            const c = clients[cId];

            const res = await fetch(`${API_BASE}/api/auth/verify-otp`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email, otp })
            });

            if (!res.ok) return alert("Geçersiz OTP!");

            const data = await res.json();
            
            setInterval(() => refreshJwtToken(cId), 14 * 60 * 1000);
            
            let savedPrivateKey = localStorage.getItem(`rsa_private_${email}`);
            
            if (!savedPrivateKey) {
                if (!data.encryptedPrivateKey) {
                    alert("Sunucuda yedeklenmiş bir anahtar bulunamadı. Lütfen önce kayıt olun.");
                    return;
                }
                
                const pin = prompt("Eski mesajlarınızı kurtarmak için Kurtarma Parolanızı girin (Unuttuysanız 'iptal' deyin):");
                if (pin) {
                    try {
                        const decryptedBytes = CryptoJS.AES.decrypt(data.encryptedPrivateKey, pin);
                        const decryptedKey = decryptedBytes.toString(CryptoJS.enc.Utf8);
                        if (!decryptedKey || !decryptedKey.includes('RSA PRIVATE KEY')) throw new Error("Yanlış parola");
                        
                        savedPrivateKey = decryptedKey;
                        c.rsa.setPrivateKey(savedPrivateKey);
                        localStorage.setItem(`rsa_private_${email}`, savedPrivateKey);
                        alert("Anahtarlarınız kurtarıldı! Eski mesajlarınızı artık okuyabilirsiniz.");
                    } catch(e) {
                        alert("Parola yanlış! Lütfen tekrar giriş yapın.");
                        return;
                    }
                } else {
                    if (confirm("Parolayı girmediniz. Yeni şifreleme anahtarı üretilecek ve ESKİ MESAJLARINIZ SİLİNECEK! Onaylıyor musunuz?")) {
                        const newPin = prompt("Yeni bir kurtarma parolası belirleyin:");
                        if (!newPin) return alert("İşlem iptal edildi.");
                        
                        c.rsa.getKey();
                        savedPrivateKey = c.rsa.getPrivateKey();
                        const newPublicKey = c.rsa.getPublicKey();
                        const newEncryptedPrivateKey = CryptoJS.AES.encrypt(savedPrivateKey, newPin).toString();
                        
                        localStorage.setItem(`rsa_private_${email}`, savedPrivateKey);
                        c.rsa.setPrivateKey(savedPrivateKey);
                        
                        const resetRes = await fetch(`${API_BASE}/api/auth/reset-keys`, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${data.token}` },
                            body: JSON.stringify({ publicKey: newPublicKey, encryptedPrivateKey: newEncryptedPrivateKey })
                        });
                        if (!resetRes.ok) return alert("Anahtar sıfırlama başarısız oldu.");
                        alert("Anahtarlarınız sıfırlandı. Yeni mesajlar için güvenliğiniz sağlandı.");
                    } else {
                        return;
                    }
                }
            } else {
                c.rsa.setPrivateKey(savedPrivateKey);
            }

            let deviceId = localStorage.getItem(`deviceId${cId}`);
            if (!deviceId) {
                deviceId = crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(36).substring(2, 15);
                localStorage.setItem(`deviceId${cId}`, deviceId);
            }
            c.deviceId = deviceId;
            c.token = data.token;
            c.refreshToken = data.refreshToken;
            c.nickname = data.nickname;

            localStorage.setItem(`token${cId}`, c.token);
            localStorage.setItem(`nickname${cId}`, c.nickname);
            localStorage.setItem(`email${cId}`, email);

            document.getElementById(`auth${cId}`).style.display = 'none';
            document.getElementById(`app${cId}`).style.display = 'block';
            document.getElementById(`myName${cId}`).innerText = c.nickname;

            await fetchProfile(cId);
            await loadBlockedListUI(cId);
            await loadInbox(cId);
            await startSignalR(cId);
        }

        async function startSignalR(cId) {
            const c = clients[cId];
            if (c.connection) await c.connection.stop();
            
            c.ephemeralRsa = new JSEncrypt({default_key_size: 1024});
            c.ephemeralRsa.getKey();
            c.ephemeralPublicKey = c.ephemeralRsa.getPublicKey();

            c.connection = new signalR.HubConnectionBuilder()
                .withUrl(`${API_BASE}/chat?access_token=${c.token}&ephemeralKey=${encodeURIComponent(c.ephemeralPublicKey)}&deviceId=${c.deviceId}`)
                .withAutomaticReconnect()
                .build();

            c.connection.on("ReceiveMessage", async (msgId, from, receiverSymKey, signature, encryptedPayload, replyToId) => {
                if (!c.lastMessageId || msgId > c.lastMessageId) c.lastMessageId = msgId;

                let aesKey = null;
                if (c.ephemeralRsa) aesKey = c.ephemeralRsa.decrypt(receiverSymKey);
                if (!aesKey) aesKey = c.rsa.decrypt(receiverSymKey);
                if (!aesKey) return;

                const decryptedBytes = CryptoJS.AES.decrypt(encryptedPayload, aesKey);
                let originalText = decryptedBytes.toString(CryptoJS.enc.Utf8);
                
                let isSignatureValid = false;
                try {
                    const senderPublicKey = await getPublicKey(cId, from);
                    if (senderPublicKey && signature) {
                        const verifier = new JSEncrypt();
                        verifier.setPublicKey(senderPublicKey);
                        isSignatureValid = verifier.verify(originalText, signature, CryptoJS.SHA256);
                    }
                } catch(e) {}
                
                if (!isSignatureValid) {
                    originalText = "[❌ İMZA GEÇERSİZ] " + originalText;
                }

                if (document.getElementById(`to${cId}`).value === from) {
                    renderMessage(cId, msgId, from, originalText, new Date().toISOString(), {}, replyToId, false, false);
                    c.connection.invoke("MarkMessagesAsRead", from).catch(console.error);
                }
                await loadInbox(cId);
            });

            c.connection.on("ReceiveWebRTCSignal", (from, payload) => {
                if (from !== c.nickname) {
                    handleWebRTC(cId, from, payload);
                }
            });

            c.connection.on("ReceiveReaction", (msgId, reactorNick, emoji) => {
                const box = document.getElementById(`msgBox_${cId}_${msgId}`);
                if (box) loadHistory(cId);
            });

            c.connection.on("MessageDeleted", (msgId) => {
                const box = document.getElementById(`msgBox_${cId}_${msgId}`);
                if (box) loadHistory(cId);
                loadInbox(cId);
            });

            c.connection.on("MessagesRead", (readerNickname) => {
                if (document.getElementById(`to${cId}`).value === readerNickname) {
                    loadHistory(cId);
                }
            });

            c.connection.on("UserPresenceUpdate", (nickname, isOnline, lastSeen) => {
                if (document.getElementById(`to${cId}`).value === nickname) {
                    updatePresenceUI(cId, isOnline, lastSeen);
                }
            });

            await c.connection.start();
            if (c.lastMessageId) {
                c.connection.invoke("SyncMessages", c.lastMessageId).catch(console.error);
            }
            addSysLog(cId, `✅ E2EE Soket Bağlantısı Sağlandı`);
        }


        async function fetchProfile(cId) {
            const c = clients[cId];
            const res = await fetch(`${API_BASE}/api/user/${c.nickname}/profile`, {
                headers: { 'Authorization': `Bearer ${c.token}` }
            });
            const data = await res.json();
            if (data.profilePictureBase64) {
                document.getElementById(`myPic${cId}`).src = data.profilePictureBase64;
            }
        }

        async function uploadPic(cId, input) {
            const file = input.files[0];
            if (!file) return;

            const reader = new FileReader();
            reader.onload = async function(e) {
                const base64 = e.target.result;
                const c = clients[cId];

                const res = await fetch(`${API_BASE}/api/user/profile-picture`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${c.token}` },
                    body: JSON.stringify({ base64Image: base64 })
                });

                if (res.ok) {
                    document.getElementById(`myPic${cId}`).src = base64;
                    alert("Profil fotoğrafı güncellendi!");
                }
            };
            reader.readAsDataURL(file);
        }

        function debounceSearchContacts(cId) {
            const c = clients[cId];
            if (c.searchTimer) clearTimeout(c.searchTimer);
            c.searchTimer = setTimeout(() => searchContacts(cId), 300);
        }

        async function searchContacts(cId) {
            const c = clients[cId];
            const query = document.getElementById(`contactSearch${cId}`).value.trim();
            const resultsDiv = document.getElementById(`searchResults${cId}`);

            if (!query) {
                resultsDiv.style.display = 'none';
                return;
            }

            try {
                const res = await fetch(`${API_BASE}/api/user/contacts?query=${encodeURIComponent(query)}`, {
                    headers: { 'Authorization': `Bearer ${c.token}` }
                });
                
                if (!res.ok) return;
                const contactsData = await res.json();
                const contacts = contactsData.items || [];
                
                resultsDiv.innerHTML = '';
                if (contacts.length === 0) {
                    resultsDiv.innerHTML = '<div style="padding:10px; color:#aaa; font-size:12px;">Sonuç bulunamadı.</div>';
                } else {
                    contacts.forEach(u => {
                        const picSrc = u.profilePictureBase64 || 'data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=';
                        resultsDiv.innerHTML += `
                            <div class="inbox-item" style="padding:8px 10px; cursor:pointer; border-bottom:1px solid #444;" onclick="startChatWith('${cId}', '${u.nickname}')">
                                <img src="${picSrc}" style="width:30px; height:30px;">
                                <div class="inbox-details">
                                    <span class="inbox-name">${u.nickname}</span>
                                </div>
                            </div>
                        `;
                    });
                }
                resultsDiv.style.display = 'block';
            } catch (err) {
                console.error("searchContacts JS hatası:", err);
            }
        }

        function startChatWith(cId, nickname) {
            document.getElementById(`contactSearch${cId}`).value = '';
            document.getElementById(`searchResults${cId}`).style.display = 'none';
            document.getElementById(`to${cId}`).value = nickname;
            loadHistory(cId);
            updateBlockButtonState(cId, nickname);
        }

        async function loadInbox(cId) {
            const c = clients[cId];
            const res = await fetch(`${API_BASE}/api/user/inbox`, {
                headers: { 'Authorization': `Bearer ${c.token}` }
            });
            if(!res.ok) return;
            const inboxData = await res.json();

            const inboxContainer = document.getElementById(`inboxList${cId}`);
            const fragment = document.createDocumentFragment();

            for (const item of inboxData) {
                const msg = item.lastMessage;
                let snippet = "[Şifreli Mesaj]";

                if (msg.isDeleted) {
                    snippet = "🚫 Bu mesaj silindi.";
                } else {
                    let aesKey = null;
                    if (msg.senderNickname === c.nickname) aesKey = c.rsa.decrypt(msg.senderEncryptedSymKey);
                    else if (msg.receiverNickname === c.nickname) {
                        if (c.ephemeralRsa) aesKey = c.ephemeralRsa.decrypt(msg.receiverEncryptedSymKey);
                        if (!aesKey) aesKey = c.rsa.decrypt(msg.receiverEncryptedSymKey);
                    }

                    if (aesKey) {
                        const decryptedBytes = CryptoJS.AES.decrypt(msg.encryptedPayload, aesKey);
                        snippet = decryptedBytes.toString(CryptoJS.enc.Utf8);

                        let isSignatureValid = false;
                        try {
                            const senderPublicKey = await getPublicKey(cId, msg.senderNickname);
                            if (senderPublicKey && msg.digitalSignature) {
                                const verifier = new JSEncrypt();
                                verifier.setPublicKey(senderPublicKey);
                                isSignatureValid = verifier.verify(snippet, msg.digitalSignature, CryptoJS.SHA256);
                            }
                        } catch(e) {}
                        
                        if (!isSignatureValid) {
                            snippet = "❌ " + snippet;
                        }

                        if (snippet.startsWith('[IMAGE]')) snippet = "📷 Fotoğraf";
                        else if (snippet.startsWith('[AUDIO]')) snippet = "🎤 Sesli Mesaj";
                        else if (snippet.startsWith('[WEBRTC_')) snippet = "📞 Sesli Çağrı";
                    }
                }

                const picSrc = item.profilePictureBase64 || 'data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=';
                const unreadBadge = item.unreadCount > 0 ? `<span style="background:red; color:white; border-radius:50%; padding:2px 6px; font-size:10px; margin-left:5px;">${item.unreadCount}</span>` : '';
                
                const div = document.createElement('div');
                div.className = 'inbox-item';
                div.onclick = () => { 
                    document.getElementById(`to${cId}`).value = item.contactNickname; 
                    loadHistory(cId); 
                    updateBlockButtonState(cId, item.contactNickname);
                };
                div.innerHTML = `
                    <img src="${picSrc}">
                    <div class="inbox-details">
                        <span class="inbox-name">${item.contactNickname} ${unreadBadge}</span>
                        <span class="inbox-msg">${snippet.substring(0, 30)}...</span>
                    </div>
                `;
                fragment.appendChild(div);
            }
            
            inboxContainer.innerHTML = '';
            inboxContainer.appendChild(fragment);
        }

        async function loadHistory(cId) {
            const c = clients[cId];
            const target = document.getElementById(`to${cId}`).value;
            if (!target) return;

            document.getElementById(`log${cId}`).innerHTML = ''; 
            c.messages = {}; 
            
            const res = await fetch(`${API_BASE}/api/message/history/${target}?limit=50`, {
                headers: { 'Authorization': `Bearer ${c.token}` }
            });
            const historyData = await res.json();
            const history = historyData.items || [];

            try {
                const pRes = await fetch(`${API_BASE}/api/user/${target}/profile`, {
                    headers: { 'Authorization': `Bearer ${c.token}` }
                });
                if (pRes.ok) {
                    const pData = await pRes.json();
                    updatePresenceUI(cId, pData.isOnline, pData.lastSeen); 
                }
            } catch(e){}

            if (c.connection) {
                c.connection.invoke("MarkMessagesAsRead", target).catch(console.error);
            }

            for (const msg of history) {
                if (msg.isDeleted) {
                    renderMessage(cId, msg.id, msg.senderNickname, "", msg.timestamp, {}, msg.replyToMessageId, true);
                    continue;
                }

                let aesKey = null;
                if (msg.senderNickname === c.nickname && msg.senderEncryptedSymKey) {
                    aesKey = c.rsa.decrypt(msg.senderEncryptedSymKey);
                } else if (msg.receiverNickname === c.nickname && msg.receiverEncryptedSymKey) {
                    if (c.ephemeralRsa) aesKey = c.ephemeralRsa.decrypt(msg.receiverEncryptedSymKey);
                    if (!aesKey) aesKey = c.rsa.decrypt(msg.receiverEncryptedSymKey);
                }

                if (aesKey) {
                    const decryptedBytes = CryptoJS.AES.decrypt(msg.encryptedPayload, aesKey);
                    let originalText = decryptedBytes.toString(CryptoJS.enc.Utf8);
                    
                    if (originalText.startsWith('[WEBRTC_')) continue;

                    let isSignatureValid = false;
                    try {
                        const senderPublicKey = await getPublicKey(cId, msg.senderNickname);
                        if (senderPublicKey && msg.digitalSignature) {
                            const verifier = new JSEncrypt();
                            verifier.setPublicKey(senderPublicKey);
                            isSignatureValid = verifier.verify(originalText, msg.digitalSignature, CryptoJS.SHA256);
                        }
                    } catch(e) {}
                    
                    if (!isSignatureValid) {
                        originalText = "[❌ İMZA GEÇERSİZ] " + originalText;
                    }
                    
                    renderMessage(cId, msg.id, msg.senderNickname, originalText, msg.timestamp, msg.reactions || {}, msg.replyToMessageId, false, msg.isRead);
                } else {
                    addSysLog(cId, `[ŞİFRELİ GEÇMİŞ] Mesaj çözülemedi`, true);
                }
            }
        }

        async function clearHistory(cId) {
            const c = clients[cId];
            const target = document.getElementById(`to${cId}`).value;
            if (!target) return alert("Sohbeti silmek için önce bir kişi seçin.");

            if (!confirm(`${target} ile olan sohbeti tamamen silmek istediğinize emin misiniz?`)) return;

            try {
                const res = await fetch(`${API_BASE}/api/message/history/${target}`, {
                    method: 'DELETE',
                    headers: { 'Authorization': `Bearer ${c.token}` }
                });

                if (res.ok) {
                    document.getElementById(`log${cId}`).innerHTML = '';
                    c.messages = {};
                    document.getElementById(`to${cId}`).value = '';
                    const el = document.getElementById(`presence${cId}`);
                    if (el) el.innerHTML = '';
                    alert("Sohbet temizlendi.");
                    await loadInbox(cId);
                } else {
                    alert("Sohbet silinirken hata oluştu.");
                }
            } catch (err) {
                alert("Hata: " + err.message);
            }
        }

        async function loadBlockedListUI(cId) {
            const c = clients[cId];
            const res = await fetch(`${API_BASE}/api/user/blocked`, {
                headers: { 'Authorization': `Bearer ${c.token}` }
            });
            if (res.ok) {
                c.blocked = await res.json();
                const container = document.getElementById(`blockedList${cId}`);
                container.innerHTML = '';
                if(c.blocked.length === 0) {
                    container.innerHTML = '<span style="color:#666; font-size:12px; padding:5px;">Engellenen kimse yok.</span>';
                }
                c.blocked.forEach(bUser => {
                    container.innerHTML += `
                        <div class="blocked-user">
                            ${bUser} 
                            <button style="width:auto; padding:2px 5px; font-size:10px; background:#28a745; margin:0;" onclick="unblockDirect(${cId}, '${bUser}')">Kaldır</button>
                        </div>
                    `;
                });
                
                const target = document.getElementById(`to${cId}`).value;
                if(target) updateBlockButtonState(cId, target);
            }
        }

        async function toggleBlock(cId) {
            const c = clients[cId];
            const target = document.getElementById(`to${cId}`).value;
            if (!target) return alert("Önce bir sohbet seçin.");

            const isBlocked = c.blocked.includes(target);
            const endpoint = isBlocked ? `/api/user/unblock/${target}` : `/api/user/block/${target}`;

            const res = await fetch(`${API_BASE}${endpoint}`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${c.token}` }
            });

            if (res.ok) {
                alert(isBlocked ? "Engel kaldırıldı." : "Kullanıcı engellendi.");
                await loadBlockedListUI(cId);
                await loadInbox(cId);
            }
        }

        async function unblockDirect(cId, target) {
            const c = clients[cId];
            const res = await fetch(`${API_BASE}/api/user/unblock/${target}`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${c.token}` }
            });

            if (res.ok) {
                alert("Engel kaldırıldı.");
                await loadBlockedListUI(cId);
                await loadInbox(cId);
            }
        }

        function updateBlockButtonState(cId, targetNickname) {
            const c = clients[cId];
            const btn = document.getElementById(`blockBtn${cId}`);
            if (c.blocked.includes(targetNickname)) {
                btn.innerText = "Engeli Kaldır";
                btn.style.background = "#28a745";
            } else {
                btn.innerText = "Engelle";
                btn.style.background = "#dc3545";
            }
        }
    