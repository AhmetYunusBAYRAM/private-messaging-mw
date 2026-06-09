# 🔒 PRIVATE.MESSAGING.MW

> **[English](#english) | [Türkçe](#türkçe)**

---

<a name="english"></a>
## 🇬🇧 English

**An end-to-end encrypted (E2EE) real-time private messaging middleware.**  
Built on ASP.NET Core, SignalR, and MongoDB with a clean N-Tier architecture.

### 📸 Client Preview

![Client Preview](./docs/client-preview.png)

---

### 📐 Architecture

```
PRIVATE.MESSAGING.MW/
├── PRIVATE.MESSAGING.Core/       # Entities & Interfaces
├── PRIVATE.MESSAGING.DTOs/       # Request / Response models
├── PRIVATE.MESSAGING.Services/   # Business Logic
└── PRIVATE.MESSAGING.MW/         # API Layer (Controllers, SignalR Hub)
```

**Dependency flow:**
```
API (MW) → Services → Core
DTOs       ↗
```

---

### 🚀 Features

#### 🔐 Authentication
- Email + OTP-based registration and login
- Session management via JWT Bearer tokens
- Asymmetric RSA + AES hybrid key management
- Key pair reset support

#### 💬 Messaging (SignalR)
- **End-to-end encryption (E2EE):** Every message is protected by an AES key encrypted with the recipient's RSA public key
- Real-time message delivery
- Read receipts (`IsRead`, `ReadAt`)
- Delete for everyone
- Clear chat history (delete for me only — `DeletedFor` soft-delete)
- Emoji reaction system

#### 👤 User Management
- Profile picture (Base64)
- Online/offline presence and last-seen timestamp
- Block / unblock users
- Device logs (IP address, device name, activity time)

#### 📋 Inbox
- Unread message counter
- Last message snippet (decrypted on client)
- Profile picture hidden for blocked users

---

### 🌐 API Endpoints

#### Auth — `/api/auth`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/register` | Register a new user (sends OTP) |
| `POST` | `/login` | Login (sends OTP) |
| `POST` | `/verify-otp` | Verify OTP, returns JWT |
| `POST` | `/reset-keys` 🔒 | Reset RSA key pair |
| `GET` | `/publickey/{nickname}` 🔒 | Get user's public key |

#### User — `/api/user`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/profile-picture` 🔒 | Upload / update profile picture |
| `GET` | `/{nickname}/profile` 🔒 | Get user profile |
| `GET` | `/contacts?query=` 🔒 | Search users (max 7 results) |
| `GET` | `/inbox` 🔒 | DM inbox list |
| `POST` | `/block/{targetNickname}` 🔒 | Block a user |
| `POST` | `/unblock/{targetNickname}` 🔒 | Unblock a user |
| `GET` | `/blocked` 🔒 | List blocked users |

#### Message — `/api/message`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/history/{contactNickname}` 🔒 | Fetch message history |
| `DELETE` | `/history/{contactNickname}` 🔒 | Clear chat (delete for me only) |

> 🔒 Requires JWT Bearer token.

#### SignalR Hub — `/chat`

| Client → Server | Description |
|-----------------|-------------|
| `SendPrivateMessage` | Send an E2EE message |
| `AddReaction` | Add / remove an emoji reaction |
| `DeleteMessage` | Delete a message for everyone |
| `MarkMessagesAsRead` | Mark messages as read |

| Server → Client | Description |
|-----------------|-------------|
| `ReceiveMessage` | New incoming message |
| `ReceiveReaction` | Reaction updated |
| `MessageDeleted` | Message deleted |
| `MessagesRead` | Messages marked as read |
| `UserPresenceUpdate` | Online status changed |

---

### ⚙️ Configuration

Add the following sections to `appsettings.json`:

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "PrivateMessagingDb"
  },
  "SmtpSettings": {
    "Host": "smtp.example.com",
    "Port": 587,
    "Username": "your@email.com",
    "Password": "your-password",
    "FromName": "Private Messaging"
  },
  "Jwt": {
    "Key": "your-super-secret-key-minimum-32-chars",
    "Issuer": "PrivateMessagingMW",
    "Audience": "PrivateMessagingClient"
  }
}
```

---

### 🏃 Running

```bash
cd PRIVATE.MESSAGING.MW
dotnet restore
dotnet run --project PRIVATE.MESSAGING.MW/PRIVATE.MESSAGING.MW.csproj
```

Access the test client at: `http://localhost:5000`

---

### 🛠️ Tech Stack

| Technology | Purpose |
|------------|---------|
| **ASP.NET Core 6** | Web API |
| **SignalR** | Real-time communication |
| **MongoDB** | Database |
| **JWT (HS256)** | Authentication |
| **RSA + AES** | End-to-end encryption |
| **MailKit** | SMTP email delivery |

---

### 🔑 Security Notes

- Private keys are **never stored in plain text** on the server; they are stored AES-encrypted.
- Each message is protected by AES keys encrypted separately for sender and receiver.
- Block timestamps are recorded server-side and cannot be manipulated by the client.

---
---

<a name="türkçe"></a>
## 🇹🇷 Türkçe

**Uçtan uca şifreli (E2EE) gerçek zamanlı özel mesajlaşma altyapısı.**  
ASP.NET Core, SignalR ve MongoDB üzerine inşa edilmiş, N-Tier (Katmanlı) mimariye sahip bir middleware projesidir.

### 📸 İstemci Önizleme

![İstemci Önizleme](./docs/client-preview.png)

---

### 📐 Mimari

```
PRIVATE.MESSAGING.MW/
├── PRIVATE.MESSAGING.Core/       # Varlıklar (Entities) ve Arayüzler (Interfaces)
├── PRIVATE.MESSAGING.DTOs/       # İstek/Yanıt modelleri (Request/Response)
├── PRIVATE.MESSAGING.Services/   # İş mantığı (Business Logic)
└── PRIVATE.MESSAGING.MW/         # API Katmanı (Controllers, SignalR Hub)
```

**Bağımlılık akışı:**
```
API (MW) → Services → Core
DTOs       ↗
```

---

### 🚀 Özellikler

#### 🔐 Kimlik Doğrulama
- E-posta + OTP tabanlı kayıt ve giriş
- JWT Bearer token ile oturum yönetimi
- Asimetrik RSA + AES hibrit şifreleme ile anahtar yönetimi
- Parola/anahtar sıfırlama desteği

#### 💬 Mesajlaşma (SignalR)
- **Uçtan uca şifreleme (E2EE):** Her mesaj, alıcının RSA public key'i ile şifrelenen AES anahtarıyla korunur
- Gerçek zamanlı mesaj iletimi
- Mesaj okundu bilgisi (`IsRead`, `ReadAt`)
- Mesaj silme (herkesten)
- Mesaj geçmişi temizleme (sadece kendisinden — `DeletedFor` mantıksal silme)
- Emoji reaksiyon sistemi

#### 👤 Kullanıcı Yönetimi
- Profil resmi (Base64)
- Çevrimiçi/çevrimdışı durumu ve son görülme zamanı
- Kullanıcı engelleme / engel kaldırma
- Cihaz günlükleri (IP, cihaz adı, aktiflik zamanı)

#### 📋 DM Kutusu (Inbox)
- Okunmamış mesaj sayacı
- Son mesaj özeti
- Engellenen kullanıcı için profil gizleme

---

### 🌐 API Endpointleri

#### Auth — `/api/auth`

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| `POST` | `/register` | Yeni kullanıcı kaydı (OTP gönderir) |
| `POST` | `/login` | Giriş (OTP gönderir) |
| `POST` | `/verify-otp` | OTP doğrulama, JWT döner |
| `POST` | `/reset-keys` 🔒 | RSA anahtar çifti sıfırlama |
| `GET` | `/publickey/{nickname}` 🔒 | Kullanıcının public key'ini getirir |

#### User — `/api/user`

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| `POST` | `/profile-picture` 🔒 | Profil resmi yükleme/güncelleme |
| `GET` | `/{nickname}/profile` 🔒 | Kullanıcı profili getirme |
| `GET` | `/contacts?query=` 🔒 | Kullanıcı arama (max 7 sonuç) |
| `GET` | `/inbox` 🔒 | DM kutusu listesi |
| `POST` | `/block/{targetNickname}` 🔒 | Kullanıcı engelleme |
| `POST` | `/unblock/{targetNickname}` 🔒 | Engel kaldırma |
| `GET` | `/blocked` 🔒 | Engellenen kullanıcı listesi |

#### Message — `/api/message`

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| `GET` | `/history/{contactNickname}` 🔒 | Mesaj geçmişi |
| `DELETE` | `/history/{contactNickname}` 🔒 | Mesaj geçmişini temizle (benden sil) |

> 🔒 JWT token gerektirir.

#### SignalR Hub — `/chat`

| Client → Server | Açıklama |
|-----------------|----------|
| `SendPrivateMessage` | E2EE mesaj gönderme |
| `AddReaction` | Emoji reaksiyon ekleme/kaldırma |
| `DeleteMessage` | Mesaj silme (herkesten) |
| `MarkMessagesAsRead` | Mesajları okundu işaretleme |

| Server → Client | Açıklama |
|-----------------|----------|
| `ReceiveMessage` | Yeni mesaj geldi |
| `ReceiveReaction` | Reaksiyon güncellendi |
| `MessageDeleted` | Mesaj silindi |
| `MessagesRead` | Mesajlar okundu |
| `UserPresenceUpdate` | Çevrimiçi durumu değişti |

---

### ⚙️ Yapılandırma

`appsettings.json` dosyasına aşağıdaki bölümleri ekleyin:

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "PrivateMessagingDb"
  },
  "SmtpSettings": {
    "Host": "smtp.example.com",
    "Port": 587,
    "Username": "your@email.com",
    "Password": "your-password",
    "FromName": "Private Messaging"
  },
  "Jwt": {
    "Key": "your-super-secret-key-minimum-32-chars",
    "Issuer": "PrivateMessagingMW",
    "Audience": "PrivateMessagingClient"
  }
}
```

---

### 🏃 Çalıştırma

```bash
cd PRIVATE.MESSAGING.MW
dotnet restore
dotnet run --project PRIVATE.MESSAGING.MW/PRIVATE.MESSAGING.MW.csproj
```

Test istemcisine erişmek için: `http://localhost:5000`

---

### 🛠️ Teknolojiler

| Teknoloji | Kullanım |
|-----------|----------|
| **ASP.NET Core 6** | Web API |
| **SignalR** | Gerçek zamanlı iletişim |
| **MongoDB** | Veritabanı |
| **JWT (HS256)** | Kimlik doğrulama |
| **RSA + AES** | Uçtan uca şifreleme |
| **MailKit** | SMTP e-posta gönderimi |

---

### 🔑 Güvenlik Notları

- Özel anahtarlar (Private Key) sunucuda **asla açık metin** olarak saklanmaz; AES ile şifrelenmiş halde tutulur.
- Her mesaj, alıcı ve gönderici için ayrı ayrı şifrelenmiş AES anahtarlarıyla korunur.
- Engelleme tarihi sunucu tarafında kaydedilir; istemci tarafından manipüle edilemez.
