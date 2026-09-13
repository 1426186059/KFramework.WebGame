using System.Numerics;
namespace MirEngine
{
    public struct Matrix
	{
	    private Matrix3x2 matrix;

	    public Matrix(int a)
	    {
	        matrix = Matrix3x2.Identity;
	    }

	    public Matrix(float m11, float m12, float m21, float m22, float dx, float dy)
	    {
	        matrix = new Matrix3x2(m11, m12, m21, m22, dx, dy);
	    }

	    public Matrix3x2 Identity
	    {
	        get { return Matrix3x2.Identity; }
	    }

	    public float[] Elements
	    {
	        get
	        {
	            return new float[]
	            {
	                matrix.M11, matrix.M12,
	                matrix.M21, matrix.M22,
	                matrix.M31, matrix.M32
	            };
	        }
	    }

	    public void Rotate(float angle)
	    {
	        matrix *= Matrix3x2.CreateRotation(angle);
	    }

	    public void Translate(float dx, float dy)
	    {
	        matrix *= Matrix3x2.CreateTranslation(dx, dy);
	    }

	    public void Scale(float scaleX, float scaleY)
	    {
	        matrix *= Matrix3x2.CreateScale(scaleX, scaleY);
	    }

	    public void Multiply(Matrix matrix)
	    {
	        this.matrix *= matrix.matrix;
	    }

	    public void Invert()
	    {
	        if (Matrix3x2.Invert(this.matrix, out Matrix3x2 result))
	        {
	            this.matrix = result;
	        }
	        else
	        {
	            // throw new InvalidOperationException("Matrix is not invertible.");
	        }
	    }
	}

}
