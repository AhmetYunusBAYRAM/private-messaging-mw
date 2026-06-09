
        function createClientUI(id) {
            document.write(`
            <div class="client-box" id="clientBox${id}">
                <h2>Kullanıcı ${id} <button class="logout-btn" onclick="logout(${id})">Çıkış Yap</button></h2>
                
                <!-- INCOMING CALL MODAL -->
                <div id="incomingCallModal${id}" style="display:none; position:absolute; top:20px; left:50%; transform:translateX(-50%); background:#28a745; color:white; padding:15px; border-radius:8px; z-index:1001; box-shadow:0 4px 10px rgba(0,0,0,0.5); text-align:center;">
                    <div id="incomingCallText${id}" style="margin-bottom:10px; font-weight:bold;">Arama Geliyor...</div>
                    <button style="background:#007bff; padding:5px 15px; font-size:12px; width:auto; border-radius:20px; margin-right:5px;" onclick="answerCall(${id})">Cevapla</button>
                    <button style="background:#dc3545; padding:5px 15px; font-size:12px; width:auto; border-radius:20px;" onclick="rejectCall(${id})">Reddet</button>
                </div>

                <!-- ACTIVE CALL MODAL -->
                <div id="callModal${id}" style="display:none; position:absolute; top:20px; left:50%; transform:translateX(-50%); background:#005c4b; color:white; padding:15px; border-radius:8px; z-index:1000; box-shadow:0 4px 10px rgba(0,0,0,0.5); text-align:center; min-width: 250px;">
                    <div id="callModalText${id}" style="margin-bottom:10px; font-weight:bold;">Arama Sürüyor...</div>
                    <audio id="remoteAudio${id}" autoplay controls style="width: 100%; height: 40px; margin-bottom: 10px;"></audio>
                    <div class="row" style="justify-content:center;">
                        <button id="muteBtn${id}" style="background:#ffc107; color:black; padding:5px 15px; font-size:12px; width:auto; border-radius:20px;" onclick="toggleMute(${id})">🔇 Sustur</button>
                        <button style="background:#dc3545; padding:5px 15px; font-size:12px; width:auto; border-radius:20px;" onclick="endCall(${id})">Kapat</button>
                    </div>
                </div>

                <!-- AUTH SECTION -->
                <div id="auth${id}">
                    <h4>1. Giriş / Kayıt</h4>
                    <input type="email" id="email${id}" placeholder="Email Adresi">
                    <input type="text" id="nick${id}" placeholder="Nickname (Sadece Register için)">
                    <div class="row">
                        <button onclick="requestOtp(${id}, 'login')">Giriş Yap (Login)</button>
                        <button onclick="requestOtp(${id}, 'register')" style="background:#28a745;">Kayıt Ol (Register)</button>
                    </div>
                    
                    <h4 style="margin-top:15px;">2. OTP Doğrulama</h4>
                    <input type="text" id="otp${id}" placeholder="Konsola gelen 6 haneli OTP">
                    <button onclick="verifyOtp(${id})" style="background:#17a2b8;">Doğrula & Sisteme Gir</button>
                </div>

                <!-- APP SECTION -->
                <div id="app${id}" style="display:none;">
                    <div class="profile-header">
                        <img id="myPic${id}" src="data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=">
                        <div style="flex-grow: 1;">
                            <h3 style="border:none; margin:0;">Hoşgeldin <span id="myName${id}"></span></h3>
                            <input type="file" style="font-size:10px; width:180px;" onchange="uploadPic(${id}, this)" accept="image/*">
                        </div>
                        <button onclick="showSessions(${id})" style="width:auto; padding:5px; font-size:11px; background:#17a2b8;">📱 Cihazlarım</button>
                    </div>

                    <div style="display:flex; justify-content:space-between; align-items:center; position:relative;">
                        <h4 style="border:none; margin:0; margin-bottom:5px;">Rehber (Kişi Ara)</h4>
                    </div>
                    <div style="position:relative; margin-bottom:15px;">
                        <input type="text" id="contactSearch${id}" placeholder="Kullanıcı ara..." oninput="debounceSearchContacts(${id})" style="margin-bottom:0;">
                        <div id="searchResults${id}" style="display:none; position:absolute; top:100%; left:0; right:0; background:#333; border:1px solid #444; border-radius:4px; max-height:200px; overflow-y:auto; z-index:100; box-shadow:0 4px 6px rgba(0,0,0,0.3);">
                            <!-- Sonuçlar buraya gelecek -->
                        </div>
                    </div>

                    <h4>DM Kutusu (Inbox) <button style="width:auto; padding:3px 10px; font-size:10px; float:right;" onclick="loadInbox(${id})">Yenile</button></h4>
                    <div class="inbox-list" id="inboxList${id}">
                    </div>

                    <h4 style="margin-top:15px;">Engellenen Kişiler <button style="width:auto; padding:3px 10px; font-size:10px; float:right;" onclick="loadBlockedListUI(${id})">Yenile</button></h4>
                    <div class="inbox-list" id="blockedList${id}" style="height:60px; margin-bottom:15px;">
                    </div>

                    <h4>Sohbet Penceresi</h4>
                    <div class="row">
                        <input type="text" id="to${id}" placeholder="Kiminle Konuşulacak? (Nickname)" readonly style="background:#222; color:#888; width: 40%;">
                        <button style="width:auto; padding:8px; background:#dc3545; margin:0;" onclick="clearHistory(${id})" title="Sohbeti Sil">🗑️</button>
                        <button style="width:auto; padding:8px 12px; background:#007bff; margin:0;" onclick="startCall(${id})" title="Sesli Ara">📞</button>
                        <button id="blockBtn${id}" class="block-btn" style="width:80px; margin:0;" onclick="toggleBlock(${id})">Engelle</button>
                        <button style="width:auto; padding:8px; background:#28a745; margin:0;" onclick="verifySecurityCode(${id})" title="Güvenlik Kodunu Doğrula">🔒</button>
                    </div>
                    
                    <div id="presence${id}" style="font-size: 11px; margin-top: -8px; margin-bottom: 8px; color: #888; min-height: 15px;"></div>
                    
                    <div class="logs" id="log${id}"></div>
                    
                    <div id="replyPreview${id}" class="reply-preview">
                        <span class="close-btn" onclick="cancelReply(${id})">✕</span>
                        <div style="color:#00a884; font-weight:bold; margin-bottom:2px;">Yanıtlanıyor...</div>
                        <div id="replyText${id}" style="color:#bbb;"></div>
                    </div>

                    <div class="row" style="align-items:center;">
                        <label for="imgUpload${id}" style="cursor:pointer; background:#28a745; border-radius:4px; padding:8px 12px; margin:5px 0; display:flex; align-items:center; justify-content:center; box-sizing:border-box;" title="Resim Ekle">📎</label>
                        <input type="file" id="imgUpload${id}" accept="image/*" style="display:none;" onchange="sendImage(${id}, this)">
                        
                        <button id="micBtn${id}" style="width:auto; padding:8px 12px; background:#17a2b8; margin:5px 0;" onclick="toggleRecording(${id})" title="Sesli Mesaj (Kaydet & Gönder)">🎙️</button>
                        
                        <input type="text" id="msg${id}" placeholder="Gizli Mesajınız..." style="flex:1;">
                        <button style="width:70px; margin:0;" onclick="sendMessage(${id})">Gönder</button>
                    </div>
                </div>
            </div>
            `);
        }
    