const express = require("express");
const app = express();
const http = require("http");
const server = http.createServer(app);

/* function postData(input) {
  $.ajax({
    type: "POST",
    url: "/reverse_pca.py",
    data: { param: input },
    success: callbackFunc
  });
} */

function callbackFunc(response) {
  // do something with the response
  console.log(response);
}

server.listen(3000, "0.0.0.0", () => {
  console.log("Server running on http://0.0.0.0:3000");
});

app.get("/", (req, res) => res.send('Signal Server Running!'));

const webSocket = require("ws");
const wss = new webSocket.Server({ server });

wss.on("connection", function (socket) {
    // Some feedback on the console
    console.log("A client just connected");
    //postData(msg);

    socket.on("message", function (msg) {
        console.log("Received message from client: " + msg);
        
        // Broadcast that message to all connected clients except sender
        wss.clients.forEach(function (client) {
          if (client !== socket) {
            console.log("--> Message Broadcasted to a client.");
            client.send(msg);
          } else {
            console.log("--- Message Skipped (sender).");
          }
        });
    });

    socket.on("close", function () {
        console.log("Client disconnected");
    });
});