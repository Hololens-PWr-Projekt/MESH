using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

class ObjMerger
{
    public static async System.Threading.Tasks.Task ReceiveAndMergeFromWebSocketAsync(string outputFile)
    {
        int vertexOffset = 0;
        List<string> mergedVertices = new List<string>();
        List<string> mergedFaces = new List<string>();

        using (ClientWebSocket ws = new ClientWebSocket())
        {
            Uri serverUri = new Uri("wss://your-websocket-server");
            await ws.ConnectAsync(serverUri, CancellationToken.None);

            Console.WriteLine("Connected to WebSocket server.");

            byte[] buffer = new byte[1024 * 4];

            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                string[] lines = message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    if (line.StartsWith("v "))
                    {
                        // Add vertex to merged list
                        mergedVertices.Add(line);
                    }
                    else if (line.StartsWith("f "))
                    {
                        // Adjust face indices and add to merged list
                        string[] parts = line.Split(' ');
                        List<string> adjustedFace = new List<string> { "f" };

                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (parts[i].Contains('/'))
                            {
                                string[] subParts = parts[i].Split('/');
                                int vertexIndex = int.Parse(subParts[0]) + vertexOffset;
                                string adjustedFacePart = vertexIndex.ToString();

                                if (subParts.Length > 1)
                                {
                                    adjustedFacePart += "/" + subParts[1];
                                }

                                if (subParts.Length > 2)
                                {
                                    adjustedFacePart += "/" + subParts[2];
                                }

                                adjustedFace.Add(adjustedFacePart);
                            }
                            else
                            {
                                int vertexIndex = int.Parse(parts[i]) + vertexOffset;
                                adjustedFace.Add(vertexIndex.ToString());
                            }
                        }

                        mergedFaces.Add(string.Join(" ", adjustedFace));
                    }
                }

                if (result.EndOfMessage)
                {
                    Console.WriteLine("End of OBJ message received.");
                    using (StreamWriter writer = new StreamWriter(outputFile))
                    {
                        writer.WriteLine("# Merged OBJ File");

                        foreach (var vertex in mergedVertices)
                        {
                            writer.WriteLine(vertex);
                        }

                        foreach (var face in mergedFaces)
                        {
                            writer.WriteLine(face);
                        }
                    }

                    Console.WriteLine("WebSocket data merged successfully into " + outputFile);
                    Console.WriteLine("Sending data back.");
                    // Send the merged OBJ file back via WebSocket
                    string mergedData = File.ReadAllText(outputFile);
                    byte[] mergedDataBytes = Encoding.UTF8.GetBytes(mergedData);
                    await ws.SendAsync(new ArraySegment<byte>(mergedDataBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }

                // Update vertex offset after processing the message
                vertexOffset = mergedVertices.Count;
            }
        }
        if (ws.State == WebSocketState.CloseReceived)
        {
            Console.WriteLine("Connection closed by client.");
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing after client", CancellationToken.None);
        }
    }

    public static async System.Threading.Tasks.Task Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; // Ensure '.' as decimal separator

        var directory = System.AppContext.BaseDirectory.Split(Path.DirectorySeparatorChar);
        var slice = new ArraySegment<string>(directory, 0, directory.Length - 4);
        var path = Path.Combine(slice.ToArray());

        Console.WriteLine($"Path of Program.cs is: {path}");

        string outputFile = Path.Combine(path, "merged_websocket.obj");
        await ReceiveAndMergeFromWebSocketAsync(outputFile);
    }
}
