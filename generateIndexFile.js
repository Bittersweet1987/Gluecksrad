// Dieses Script wird von settings.html aufgerufen, um eine neue index.html zu generieren
async function generateIndexFileFromSettings(config) {
    // Vollständiges Template aus @index.html
    const template = `<html>
    <head>
		<meta charset="UTF-8">
        <title>Gluecksrad</title>
        <link rel="stylesheet" href="main.css" type="text/css" />
        <script type="text/javascript" src="Winwheel.js"></script>
        <script src="https://cdnjs.cloudflare.com/ajax/libs/gsap/2.1.3/TweenMax.min.js"></script>
        <script>
            // Konfiguration direkt eingebettet
            const ACTION_ID = "b6e91213-a8fa-437e-8b46-a4208529fb45";
const blinkConfig = {
    "duration": 10000,
    "interval": 300,
    "color": "#19cfe5"
};
const wheelMode = {
    "mode": 1
};
const wheelConfig = {
    "numSegments": 0,
    "lineWidth": 9,
    "strokeStyle": "#6441a5",
    "outerRadius": 310,
    "innerRadius": 80,
    "textAlignment": "outer",
    "textOrientation": "vertical",
    "rotationAngle": 0,
    "drawText": false,
    "drawMode": "segmentImage",
    "imageOverlay": true,
    "imageDirection": "N",
    "animation": {
        "type": "spinToStop",
        "duration": 35,
        "spins": 25,
        "callbackFinished": null,
        "callbackAfter": null
    },
    "pins": {
        "number": 16,
        "outerRadius": 5,
        "margin": 0,
        "fillStyle": "#080808",
        "strokeStyle": "#ffffff"
    },
    "textPadding": 16
};
const priceSegment = [
    {
        "image": "image/ToreRad2.png",
        "fillStyle": "#009925",
        "textFontSize": 16,
        "textFillStyle": "#0000ff",
        "prize": "Test1",
        "message": "Geh aufs Ganze",
        "text": "",
        "probability": 14
    },
    {
        "image": "image/Zonk3.png",
        "fillStyle": "#d50f25",
        "textFontSize": 16,
        "textFillStyle": "black",
        "prize": "Test2",
        "message": "Zonk",
        "text": "",
        "probability": 12
    },
    {
        "image": "image/FreeSpin2.png",
        "fillStyle": "#ffff99",
        "textFontSize": 16,
        "textFillStyle": "black",
        "prize": "Test3",
        "message": "Free Spin",
        "text": "",
        "probability": 15
    },
    {
        "image": "image/Zonk3.png",
        "fillStyle": "#d50f25",
        "textFontSize": 16,
        "textFillStyle": "black",
        "prize": "Test4",
        "message": "Zonk",
        "text": "",
        "probability": 12
    },
    {
        "image": "image/Pushups2.png",
        "fillStyle": "#009925",
        "textFontSize": 16,
        "textFillStyle": "black",
        "prize": "Test5",
        "message": "Pushups",
        "text": "",
        "probability": 10
    },
    {
        "image": "image/Zonk3.png",
        "fillStyle": "#d50f25",
        "textFontSize": 16,
        "textFillStyle": "black",
        "prize": "Test6",
        "message": "Zonk",
        "text": "",
        "probability": 13
    },
    {
        "image": "image/10KPoints2.png",
        "fillStyle": "#ffff99",
        "textFontSize": 16,
        "textFillStyle": "black",
        "prize": "Test7",
        "message": "10000 GambaPoints",
        "text": "",
        "probability": 10
    },
    {
        "image": "image/Zonk3.png",
        "fillStyle": "#d50f25",
        "textFontSize": 16,
        "textFillStyle": "white",
        "prize": "Test8",
        "message": "Zonk",
        "text": "",
        "probability": 14
    }
]; // (Hier steht der Segment-Array-Inhalt, wie im Original)
        </script>
    </head>
    <body>
	<!-- /* Kann beliebig erweitert werden */ -->
        <div align="center">
             <table cellpadding="0" cellspacing="0" border="0">
                <tr>
                    <td width="438" height="582" class="the_wheel" align="center" valign="center">
                        <canvas id="canvas" width="1000" height="1000" align="center" valign="center">
                            <p style="color: white;" align="center">Sorry, your browser doesn't support canvas. Please try another.</p>
                        </canvas>
                    </td>
                </tr>
            </table>
        </div>
        <script>
        // Unterdrücke die GSAP Debug-Meldung '--- requestAnimationFrame ---'
        const originalConsoleLog = console.log;
        console.log = function(...args) {
            if (args.length === 1 && typeof args[0] === 'string' && args[0].includes('--- requestAnimationFrame ---')) {
                return;
            }
            originalConsoleLog.apply(console, args);
        };
        </script>
        <script type="module">
			// Websocket-Client für Streamerbot
			const client = {
				onData: (data) => {
					console.log('Streamer.bot Data Received', data);
				}
			};

			let ws;
			let theWheel;
			let segColors;

			// Initialisiere das Rad sofort
			function initWheel() {
				// Setze die dynamischen Werte
				wheelConfig.numSegments = priceSegment.length;
				wheelConfig.rotationAngle = startRotation;
				wheelConfig.animation.callbackFinished = alertPrize;
				wheelConfig.animation.callbackAfter = drawColourTriangle;
				
				// Setze die Segment-Größen basierend auf dem Modus
				if (wheelMode.mode === 1) {
					// Modus 1: Segmentgröße basierend auf Wahrscheinlichkeit (dynamisch)
					const totalProbability = priceSegment.reduce((sum, seg) => sum + seg.probability, 0);
					for (let i = 0; i < priceSegment.length; i++) {
						priceSegment[i].size = 360 * (priceSegment[i].probability / totalProbability);
					}
				} else {
					// Modus 2: Gleiche Segmentgröße (dynamisch)
					const equalSize = 360 / priceSegment.length;
					for (let i = 0; i < priceSegment.length; i++) {
						priceSegment[i].size = equalSize;
					}
				}

				// Erstelle das Wheel mit der Konfiguration
				theWheel = new Winwheel({
					...wheelConfig,
					segments: priceSegment
				});
				
				// Initialisiere segColors nach der Wheel-Erstellung
				segColors = theWheel.segments;
				
				// Zeichne das Rad
				theWheel.draw();
				drawColourTriangle();
			}

			// Auslesen der vorherigen StopPosition
			let startRotation = 0;
			let oldStopAngle = localStorage.getItem('lastAngle');

			if (oldStopAngle) {
				let savedAngleJson = JSON.parse(oldStopAngle);
				startRotation = savedAngleJson.Deg;
			}

			// Initialisiere das Rad sofort
			initWheel();

			function connectws() {
				if ("WebSocket" in window) {
					ws = new WebSocket("ws://127.0.0.1:8080");
					bindEvents();
				} else {
					console.error("Ihr Browser unterstützt keine WebSockets.");
				}
			}

			function bindEvents() {
				ws.onopen = () => {
					ws.send(JSON.stringify({
						request: "Subscribe",
						id: "StartListener",
						events: {
							general: ["Custom"]
						}
					}));
					console.info("WebSocket connected and subscribed to Custom events.");
				};

				ws.onmessage = (event) => {
					let wsdata;
					try {
						wsdata = JSON.parse(event.data);
					} catch (err) {
						console.error("Fehler beim Parsen der Nachricht:", err);
						return;
					}
					
					handleWebSocketData(wsdata);
				};

				ws.onclose = () => {
					console.error("WebSocket-Verbindung unerwartet geschlossen! Neuer Verbindungsversuch...");
					setTimeout(connectws, 10000);
				};
			}

			function handleWebSocketData(wsdata) {
				if (wsdata.event && wsdata.event.source === "General" && wsdata.event.type === "Custom") {
					let command = "";
					if (typeof wsdata.data === "string") {
						command = wsdata.data;
					} else if (wsdata.data && typeof wsdata.data.data === "string") {
						command = wsdata.data.data;
					}
					
					if (command === "glücksradStart") {
						startGluecksrad();
					}
				}
			}

			function startGluecksrad() {
				calculatePrize();
			}
			
			// Variablen, die vom Code in dieser Seite für die Steuerung der Leistung verwendet werden
			let wheelPower    = 0;
			let wheelSpinning = false;

			// Zeichnet das Dreieck als Pointer im Rad oben
			function drawColourTriangle()
			{
				let ctx = theWheel.ctx;
				ctx.strokeStyle = 'transparent';     
				ctx.fillStyle   = 'transparent';     
				ctx.lineWidth   = 2;				 
				ctx.beginPath();              		 
				ctx.moveTo(515, 160);           	 
				ctx.lineTo(485, 160);           	 
				ctx.lineTo(500, 210);
				ctx.lineTo(516, 160);
				ctx.stroke();                 
				ctx.fill();                   
			}
			
			function getRandomSegmentIndex(segments) {
				const totalProbability = segments.reduce((sum, segment) => sum + segment.probability, 0);
				const randomValue = Math.random() * totalProbability;

				let cumulativeProbability = 0;
				for (let i = 0; i < segments.length; i++) {
					cumulativeProbability += segments[i].probability;
					if (randomValue < cumulativeProbability) {
						return i;
					}
				}          
				return segments.length - 1;
			}
			
			function calculatePrize()
			{
				let countAngle = [];
				let sum = 0;
				let sumPerc = 0;
				let sectorBegin = 0;
				let sectorEnd = 0;
				let targetAngle = 0;
				
				// Debug-Ausgabe der Segmentgrößen
				for (let x = 1; x <= theWheel.numSegments; x++) {
					countAngle[x] = theWheel.segments[x].size;
					
				}
				
				// Berechne Summen
				for (let i = 1; i <= theWheel.numSegments; i++) {
					sum += countAngle[i];
					sumPerc += winwheelDegreesToPercent(countAngle[i]);
				}
				console.log('Gesamtsumme:', sum, 'Gesamtprozente:', sumPerc);

				const randomSegmentIndex = getRandomSegmentIndex(priceSegment);
				
				
				
				// Überprüfe, ob das Segment existiert
				if (randomSegmentIndex + 1 > theWheel.numSegments) {
					console.error('Segment-Index außerhalb des gültigen Bereichs:', randomSegmentIndex + 1);
					return;
				}
				
				sectorBegin = theWheel.segments[randomSegmentIndex + 1].startAngle + 1;
				sectorEnd = theWheel.segments[randomSegmentIndex + 1].endAngle - 1;
				
				console.log('Sektor-Bereich:', sectorBegin, sectorEnd);
				
				targetAngle = Math.random() * (sectorEnd - sectorBegin) + sectorBegin;
				console.log('Zielwinkel:', targetAngle);

				theWheel.animation.stopAngle = targetAngle;
				
				drawColourTriangle();
				
				theWheel.startAnimation();
				
				// Sende das Ergebnis an Streamerbot
				ws.send(JSON.stringify({
					request: 'DoAction',
					id: 'DoAction',
					action: {
						id: ACTION_ID,
					},
					args: {
						gewinnItem: priceSegment[randomSegmentIndex].prize,
						gewinnText: priceSegment[randomSegmentIndex].message
					}
				}));
			}			
			
			function startSpin()
			{
				if (wheelSpinning == false) {
					theWheel.startAnimation();
					wheelSpinning = true;
				}
			}

			function alertPrize(indicatedSegment)
			{
				let winningSegmentNumber = theWheel.getIndicatedSegmentNumber();
				let originalColor = theWheel.segments[winningSegmentNumber].fillStyle;
				let blinkCount = 0;
				let maxBlinks = Math.floor(blinkConfig.duration / blinkConfig.interval);

				// Gewinnersegment einfärben
				theWheel.segments[winningSegmentNumber].fillStyle = blinkConfig.color;

				// Blinken starten
				let blinkInterval = setInterval(function() {
					if (blinkCount >= maxBlinks) {
						clearInterval(blinkInterval);
						theWheel.segments[winningSegmentNumber].fillStyle = originalColor;
						theWheel.draw();
						return;
					}

					if (theWheel.segments[winningSegmentNumber].fillStyle === blinkConfig.color) {
						theWheel.segments[winningSegmentNumber].fillStyle = 'white';
					} else {
						theWheel.segments[winningSegmentNumber].fillStyle = blinkConfig.color;
					}
					theWheel.draw();
					blinkCount++;
				}, blinkConfig.interval);

				theWheel.draw();
				drawColourTriangle();

				let anglePos = theWheel.getRotationPosition();
				const jsonData = { "Deg": anglePos };
				localStorage.setItem("lastAngle", JSON.stringify(jsonData));    
			}

			// Initialisiere die Websocket-Verbindung erst nach der Wheel-Erstellung
			connectws();
        </script>	
    </body>
</html>`;

    // Kopie der Konfiguration, damit das Original nicht verändert wird
    const exportConfig = JSON.parse(JSON.stringify(config));

    // Alle Segmente anpassen: image immer mit 'image/'
    exportConfig.priceSegment = exportConfig.priceSegment.map(seg => ({
        ...seg,
        image: seg.image ? (seg.image.startsWith('image/') ? seg.image : 'image/' + seg.image.replace(/^image[\\/]/, '')) : ''
    }));

    let result = template;
    result = result.replace(
      /const\s+ACTION_ID\s*=\s*"[^"]+";/,
      `const ACTION_ID = "${exportConfig.actionId}";`
    );
    result = result.replace(
      /const\s+blinkConfig\s*=\s*{[\s\S]*?};/,
      `const blinkConfig = ${JSON.stringify(exportConfig.blinkConfig, null, 4)};`
    );
    result = result.replace(
      /const\s+wheelMode\s*=\s*{[\s\S]*?};/,
      `const wheelMode = ${JSON.stringify(exportConfig.wheelMode, null, 4)};`
    );
    result = result.replace(
      /const\s+wheelConfig\s*=\s*{[\s\S]*?};/,
      `const wheelConfig = ${JSON.stringify(exportConfig.wheelConfig, null, 4)};`
    );
    result = result.replace(
      /const\s+priceSegment\s*=\s*\[[\s\S]*?\];/,
      `const priceSegment = ${JSON.stringify(exportConfig.priceSegment, null, 4)};`
    );

    // Biete die Datei als Download an
    const blob = new Blob([result], {type: 'text/html'});
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'index.html';
    a.click();
} 