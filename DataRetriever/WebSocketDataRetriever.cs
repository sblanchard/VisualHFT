﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json;
using SuperSocket.ClientEngine;
using VisualHFT.DataRetriever.DataParsers;
using VisualHFT.Helpers;
using VisualHFT.Model;
using WebSocket4Net;

namespace VisualHFT.DataRetriever;

public class WebsocketData
{
    public string type { get; set; }
    public string data { get; set; }
}

public class WebSocketDataRetriever : IDataRetriever
{
    private const int INITIAL_DELAY = 5000; // Initial delay of 5 seconds
    private const int MAX_DELAY = 30000; // Max delay of 30 seconds
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private bool _disposed; // to track whether the object has been disposed
    private readonly IDataParser _parser;
    private WebSocket _webSocket;
    private readonly JsonSerializerSettings settings;
    private readonly string WEBSOCKET_URL = ConfigurationManager.AppSettings["WSorderBook"];


    public WebSocketDataRetriever(IDataParser parser)
    {
        _parser = parser;
        settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new CustomDateConverter() },
            DateParseHandling = DateParseHandling.None,
            DateFormatString = "yyyy.MM.dd-HH.mm.ss.ffffff"
        };
        InitializeWebSocket();
    }

    public event EventHandler<DataEventArgs> OnDataReceived;

    public async Task StartAsync()
    {
        await _webSocket.OpenAsync();
    }

    public async Task StopAsync()
    {
        await _webSocket.CloseAsync();
        _webSocket.Opened -= WebSocket_Opened;
        _webSocket.Closed -= WebSocket_Closed;
        _webSocket.Error -= WebSocket_Error;
        _webSocket.MessageReceived -= WebSocket_MessageReceived;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~WebSocketDataRetriever()
    {
        Dispose(false);
    }

    private void InitializeWebSocket()
    {
        _webSocket = new WebSocket(WEBSOCKET_URL);
        _webSocket.Opened += WebSocket_Opened;
        _webSocket.Closed += WebSocket_Closed;
        _webSocket.Error += WebSocket_Error;
        _webSocket.MessageReceived += WebSocket_MessageReceived;
    }

    private void WebSocket_Opened(object? sender, EventArgs e)
    {
        log.Info("WebSocket connection opened.");
    }

    private void WebSocket_Closed(object? sender, EventArgs e)
    {
        log.Info("WebSocket connection closed. Attempting to reconnect...");
        HandleReconnection();
    }

    private void WebSocket_Error(object? sender, ErrorEventArgs e)
    {
        log.Error($"WebSocket error: {e.Exception}");
        HandleReconnection();
    }

    private void WebSocket_MessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        var message = e.Message;
        var dataReceived = _parser.Parse<WebsocketData>(message);
        var dataType = dataReceived.type;
        object modelObj = null;

        // Determine the type of data received (e.g., market data, providers, active orders, etc.)
        if (dataType == "Market")
            modelObj = _parser.Parse<IEnumerable<OrderBook>>(dataReceived.data, settings);
        else if (dataType == "ActiveOrders")
            modelObj = _parser.Parse<List<Order>>(dataReceived.data, settings);
        else if (dataType == "Strategies")
            modelObj = _parser.Parse<List<StrategyVM>>(dataReceived.data, settings);
        else if (dataType == "Exposures")
            modelObj = _parser.Parse<List<Exposure>>(dataReceived.data, settings);
        else if (dataType == "HeartBeats")
            modelObj = _parser.Parse<List<Provider>>(dataReceived.data, settings);
        else if (dataType == "Trades")
            modelObj = _parser.Parse<List<Trade>>(dataReceived.data, settings);
        else
            log.Warn("Websocket data retriever :" + dataType + " error: NOT RECOGNIZED.");


        OnDataReceived?.Invoke(this,
            new DataEventArgs { DataType = dataType, RawData = message, ParsedModel = modelObj });
    }

    private async void HandleReconnection()
    {
        var delay = INITIAL_DELAY;
        while (true)
        {
            if (_webSocket.State == WebSocketState.Open) return;

            log.Info("Attempting to reconnect...");
            try
            {
                _webSocket.Open();
                await Task.Delay(MAX_DELAY); // Give it some time to attempt the connection
            }
            catch
            {
                log.Warn("Failed to reconnect. Retrying...");
                await Task.Delay(delay);
                delay = Math.Min(delay * 2, MAX_DELAY);
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_webSocket != null)
                    StopAsync();
                _webSocket.Dispose();
            }

            _disposed = true;
        }
    }
}