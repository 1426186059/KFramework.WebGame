namespace KFramework.MonoGame
{

    /// <summary>
    /// WebTransport(QUIC) 连接上一条流的方向。与 KFramework.TSEngine/src/net_quic.ts 的
    /// STREAM_BIDI / STREAM_UNI_SEND / STREAM_UNI_RECV 一一对应。
    /// </summary>
    public enum Net_Quic_StreamKind
    {
        /// <summary>双向流：可读可写（连接主流、本地开的双向流、服务器发起的入站双向流）。</summary>
        Bidirectional = 0,

        /// <summary>单向流 —— 本地创建：只能写，对端读（<c>OpenStreamAsync(unidirectional: true)</c>）。</summary>
        SendOnly = 1,

        /// <summary>单向流 —— 对端创建：只能读，不能往它发（服务器推送常用）。</summary>
        ReceiveOnly = 2,
    }
}
