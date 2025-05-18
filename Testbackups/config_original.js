// Streamerbot Action ID für Gewinn-Benachrichtigung
const ACTION_ID = 'b6e91213-a8fa-437e-8b46-a4208529fb45';

// Blink-Einstellungen für Gewinnersegment
const blinkConfig = {
    duration: 10000, // Dauer in Millisekunden (10 Sekunden)
    interval: 300,   // Blink-Intervall in Millisekunden
    color: '#19cfe5' // Blink-Farbe
};

// Rad-Modus
const wheelMode = {
    mode: 2 // 1 = Segmentgröße basierend auf Wahrscheinlichkeit, 2 = Gleiche Segmentgröße
};

// Wheel-Konfiguration
const wheelConfig = {
    numSegments: 0, // Wird dynamisch basierend auf priceSegment.length gesetzt
    lineWidth: 1,
    strokeStyle: '#6441a5',
    outerRadius: 310,
    innerRadius: 80,
    textFontSize: 16,
    textAlignment: 'inner',
    textOrientation: 'horizontal',
    rotationAngle: 0, // Wird dynamisch gesetzt
    drawText: true,
    drawMode: 'segmentImage',
    imageOverlay: true,
    imageDirection: 'S',
    pointerAngle: 0,
    animation: {
        type: 'spinToStop',
        duration: 35,
        spins: 25,
        callbackFinished: null, // Wird dynamisch gesetzt
        callbackAfter: null // Wird dynamisch gesetzt
    },
    pins: {
        number: 16,
        outerRadius: 5,
        margin: -5,
        fillStyle: 'transparent',
        strokeStyle: 'transparent'
    }
};

// Segment-Definitionen
const priceSegment = [
    {
        // Segment 1
        image: 'image/ToreRad2.png',
        fillStyle: '#009925',
        textFontSize: 16,
        textFillStyle: '#0000ff',
        prize: 'Test1',
        message: 'Geh aufs Ganze',
        text: '',
        size: 0, // Wird dynamisch gesetzt
        probability: 28 // Wahrscheinlichkeit in Prozent
    },
    {
        // Segment 2
        image: 'image/Zonk3.png',
        fillStyle: '#d50f25',
        textFontSize: 16,
        textFillStyle: 'black',
        prize: 'Test2',
        message: 'Zonk',
        text: '',
        size: 0, // Wird dynamisch gesetzt
        probability: 12 // Wahrscheinlichkeit in Prozent
    },
    {
        // Segment 3
        image: 'image/FreeSpin2.png',
        fillStyle: '#ffff99',
        textFontSize: 16,
        textFillStyle: 'black',
        prize: 'Test3',
        message: 'Free Spin',
        text: '',
        size: 0, // Wird dynamisch gesetzt
        probability: 1 // Wahrscheinlichkeit in Prozent
    },
    {
        // Segment 4
        image: 'image/Zonk3.png',
        fillStyle: '#d50f25',
        textFontSize: 16,
        textFillStyle: 'black',
        prize: 'Test4',
        message: 'Zonk',
        text: '',
        size: 0, // Wird dynamisch gesetzt
        probability: 12 // Wahrscheinlichkeit in Prozent
    },
    {
        // Segment 5
        image: 'image/Pushups2.png',
        fillStyle: '#009925',
        textFontSize: 16,
        textFillStyle: 'black',
        prize: 'Test5',
        message: 'Pushups',
        text: '',
        size: 0, // Wird dynamisch gesetzt
        probability: 10 // Wahrscheinlichkeit in Prozent
    },
    {
        // Segment 6
        image: 'image/Zonk3.png',
        fillStyle: '#d50f25',
        textFontSize: 16,
        textFillStyle: 'black',
        prize: 'Test6',
        message: 'Zonk',
        text: '',
        size: 0, // Wird dynamisch gesetzt
        probability: 13 // Wahrscheinlichkeit in Prozent
    },
    {
        // Segment 7
        image: 'image/10KPoints2.png',
        fillStyle: '#ffff99',
        textFontSize: 16,
        textFillStyle: 'black',
        prize: 'Test7',
        message: '10000 GambaPoints',
        text: '',
        size: 0, // Wird dynamisch gesetzt
        probability: 10 // Wahrscheinlichkeit in Prozent
    },
    {
        // Segment 8
        image: 'image/Zonk3.png',
        fillStyle: '#d50f25',
        textFontSize: 16,
        textFillStyle: 'white',
        prize: 'Test8',
        message: 'Zonk',
        text: '',
        size: 0, // Wird dynamisch gesetzt
        probability: 14 // Wahrscheinlichkeit in Prozent
    }
];

// Exportiere die Konfiguration
window.wheelConfig = wheelConfig;
window.priceSegment = priceSegment;
window.blinkConfig = blinkConfig;
window.wheelMode = wheelMode; 