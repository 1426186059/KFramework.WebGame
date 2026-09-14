using System;

namespace KFramework.Example3
{
    public class Particle:KTransform
    {
        public float DragPerSecond = 0.9f;

        public Color Color;

        public Vector2 Direction;

        public float LifeTime;

        public float InitialLifeTime;

        public Vector2 PreviousPosition;

        public float TailLength;

        Vector2 Velocity;

        public bool UseGravity = true;
        public float GravityAcceleration = 3400.0f;

        public bool IsAlive => LifeTime > 0;

        public event Action<Vector2> OnDeath;
        public Particle(Vector2 LocalPosition, Vector2 direction, float speed, float lifeTime, Color color, float scale, float tailLength = 0f)
        {
            this.LocalPosition = LocalPosition;
            this.PreviousPosition = LocalPosition;
            this.Velocity = direction * speed;
            this.LifeTime = lifeTime;
            this.InitialLifeTime = lifeTime;
            this.Color = color;
            this.LocalScale = Vector2.One * scale;
            this.TailLength = tailLength;
        }
        
        public override void Update()
        {
            var elapsedTime = KTime.deltaTime;
            if (elapsedTime <= 0)
            {
                return;
            }

            if (UseGravity)
            {
                Velocity.Y += GravityAcceleration * elapsedTime;
            }

            PreviousPosition = LocalPosition;
            LocalPosition += Velocity * elapsedTime;
            float dragFactor = Math.Max(1 - (elapsedTime * DragPerSecond), 0);
            Velocity *= dragFactor;
            LifeTime -= elapsedTime;
            Color.A = (byte)(255f * LifeTime / InitialLifeTime);
            if (!IsAlive)
            {
                OnDeath?.Invoke(LocalPosition);
            }
        }

    }
}