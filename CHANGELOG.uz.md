# O'zgarishlar Tarixi

## [3.0.1] - 2026-07-27

### 🐛 Tuzatildi

- `Flush` metodidagi poyga holati (race condition) tuzatildi — `_currentChunk` dan snapshot olish orqali
- Timeout sodir bo'lganda holatni tiklash (reset) qo'shildi

### 🚀 Optimallashtirildi

- Kanal I/O operatsiyalari baytma-bayt o'rniga `byte[]` bloklarida bajarildi

### 🔄 O'zgartirildi

- `System.IO.Ports` paketi `10.0.8` versiyasiga yangilandi

---

## [3.0.0] - 2026-04-22

### 🗑️ O'chirildi

- `MqttManagedClient` qo'llab-quvvatlashi olib tashlandi (`MqttStream`)

### ✨ Qo'shildi

- MQTT client ulanishi uchun `CancellationToken` orqali bekor qilish imkoniyati qo'shildi
- `SerialStream` sinfiga ketma-ket port parametrlarini sozlash uchun yangi xususiyatlar qo'shildi

### 🔄 O'zgartirildi

- `System.IO.Ports` paketi `10.0.7` versiyasiga yangilandi
- Boshqa bog'liqliklar yangilandi

---
