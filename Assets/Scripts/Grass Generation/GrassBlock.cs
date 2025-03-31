using UnityEngine;

namespace LenniUhr.Grass
{
    public class GrassBlock
    {
        private Bounds m_Bounds;

        private bool m_Initialized = false;
        public bool Initialized
        {
            get { return m_Initialized; }
        }

        public bool Visible = false;

        private ComputeBuffer m_SourceVertexBuffer;
        public ComputeBuffer SourceVertexBuffer
        {
            get { return m_SourceVertexBuffer; }
        }

        private GraphicsBuffer m_InfoBuffer;
        public GraphicsBuffer InfoBuffer
        {
            get { return m_InfoBuffer; }
        }

        public void Setup(Bounds bounds, ComputeBuffer sourceVertexBuffer, GraphicsBuffer infoBuffer)
        {
            m_Bounds = bounds;
            m_SourceVertexBuffer = sourceVertexBuffer;
            m_InfoBuffer = infoBuffer;
            m_Initialized = true;
        }

        public void ReleaseBuffer()
        {
            if (!m_Initialized)
                return;

            m_SourceVertexBuffer?.Release();
            m_InfoBuffer?.Release();
            m_Initialized = false;
        }

        public Bounds GetBounds()
        {
            return m_Bounds;
        }
    }
}