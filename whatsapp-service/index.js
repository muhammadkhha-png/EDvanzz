const { Client, LocalAuth } = require('whatsapp-web.js');
const express = require('express');
const qrcode = require('qrcode-terminal');

const app = express();
app.use(express.json());

// WhatsApp Client
const client = new Client({
    authStrategy: new LocalAuth(),
    puppeteer: {
        headless: true,
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    }
});

// State
let isReady = false;
let qrCodeData = null;

// QR Event
client.on('qr', (qr) => {
    qrCodeData = qr;
    isReady = false;

    qrcode.generate(qr, { small: true });
    console.log('Scan QR code');
});

// Ready Event
client.on('ready', () => {
    isReady = true;
    qrCodeData = null;
    console.log('WhatsApp is ready');
});

// Reconnect
client.on('disconnected', () => {
    isReady = false;
    client.initialize();
});

// Init
client.initialize();


// ───── SEND MESSAGE ─────
app.post('/send', async (req, res) => {
    const { phone, message } = req.body;

    if (!isReady)
        return res.status(503).json({ success: false, message: 'Not ready' });

    const chatId = phone.replace(/\D/g, '') + '@c.us';

    try {
        await client.sendMessage(chatId, message);
        res.json({ success: true });
    } catch (err) {
        res.status(500).json({ success: false, reason: err.message });
    }
});


// ───── STATUS ─────
app.get('/status', (req, res) => {
    res.json({
        ready: isReady,
        hasQr: qrCodeData !== null
    });
});


// ───── QR ─────
app.get('/qr', (req, res) => {
    res.json({
        qr: qrCodeData
    });
});


// Start server
app.listen(3000, () => {
    console.log('Server running on http://localhost:3000');
});