using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink.Utils
{
    /// <summary>
    /// Represents a non zero, positiove integer.
    /// Automatically sets to 1 if constuctor value is invalid
    /// </summary>
    public struct NonZeroPositiveInteger : IEquatable<NonZeroPositiveInteger>
    {
        private readonly int value;

        /// <summary>
        /// Constructor, will set to 1 if value is less than 1
        /// </summary>
        /// <param name="value">Initial value</param>
        public NonZeroPositiveInteger(int value)
        {
            this.value = value > 0 ? value : 1;
        }

        /// <summary>
        /// Implicitely converts to int
        /// </summary>
        /// <param name="current">Current element</param>
        public static implicit operator int(NonZeroPositiveInteger current)
        {
            return current.value;
        }

        /// <summary>
        /// Implicitely converts from int (we allow implicit as value is automatically changed to a usable one
        /// </summary>
        /// <param name="current">Current element</param>
        public static implicit operator NonZeroPositiveInteger(int current)
        {
            return new NonZeroPositiveInteger(current);
        }

        /// <summary>
        /// Checks for equality
        /// </summary>
        /// <param name="other">Other element</param>
        /// <returns>True if values are equals</returns>
        public bool Equals(NonZeroPositiveInteger other)
        {
            return this.value == other.value;
        }

        /// <see cref="object.GetHashCode"/>
        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

    }
}
