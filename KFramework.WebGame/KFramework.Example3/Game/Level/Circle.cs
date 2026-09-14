
namespace KFramework.Example3
{
    /// <summary>
    /// Represents a 2D circle.
    /// </summary>
    internal struct Circle
    {
        /// <summary>
        /// Center position of the circle.
        /// </summary>
        public Vector2 Center;

        /// <summary>
        /// Radius of the circle.
        /// </summary>
        public float Radius;

        /// <summary>
        /// Constructs a new circle.
        /// </summary>
        /// <param name="position">The Vector2 position which will the center of the circle.</param>
        /// <param name="radius">The radius of the circle.</param>
        public Circle(Vector2 position, float radius)
        {
            Center = position;
            Radius = radius;
        }

        /// <summary>
        /// Determines if this circle intersects with the specified rectangle.
        /// </summary>
        /// <param name="rectangle">The rectangle to test for intersection.</param>
        /// <returns>
        /// <c>true</c> if the circle and rectangle overlap; otherwise, <c>false</c>.
        /// </returns>
        public bool Intersects(Rectangle rectangle)
        {
            Vector2 closestPoint = new Vector2(
                MathHelper.Clamp(Center.X, rectangle.Left, rectangle.Right),
                MathHelper.Clamp(Center.Y, rectangle.Top, rectangle.Bottom)
            );

            Vector2 direction = Center - closestPoint;
            float distanceSquared = direction.LengthSquared();
            return ((distanceSquared > 0) && (distanceSquared < Radius * Radius));
        }
    }
}