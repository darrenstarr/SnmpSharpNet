// This file is part of SNMP#NET.
// 
// SNMP#NET is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// SNMP#NET is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with SNMP#NET.  If not, see <http://www.gnu.org/licenses/>.
// 

namespace SnmpSharpNet
{
    /// <summary>Helper returns error messages for SNMP v1 and v2 error codes</summary>
    /// <remarks>
    /// Helper class provides translation of SNMP version 1 and 2 error status codes to short, descriptive
    /// error messages.
    /// 
    /// To use, call the static member <see cref="SnmpError.ErrorMessage"/>.
    /// 
    /// Example:
    /// <code>Console.WriteLine("Agent error: {0}",SnmpError.ErrorMessage(12));</code>
    /// </remarks>
    public sealed class SnmpError
    {
        /// <summary>
        /// Return SNMP version 1 and 2 error code (errorCode field in the <see cref="Pdu"/> class) as
        /// a short, descriptive string.
        /// </summary>
        /// <param name="errorCode">Error code sent by the agent</param>
        /// <returns>Short error message for the error code</returns>
        public static string ErrorMessage(int errorCode)
        {
            switch (errorCode)
            {
                case SnmpConstants.ErrNoError:
                    return "No error";
                case SnmpConstants.ErrTooBig:
                    return "Request too big";
                case SnmpConstants.ErrNoSuchName:
                    return "noSuchName";
                case SnmpConstants.ErrBadValue:
                    return "badValue";
                case SnmpConstants.ErrReadOnly:
                    return "readOnly";
                case SnmpConstants.ErrGenError:
                    return "genericError";
                case SnmpConstants.ErrNoAccess:
                    return "noAccess";
                case SnmpConstants.ErrWrongType:
                    return "wrongType";
                case SnmpConstants.ErrWrongLength:
                    return "wrongLength";
                case SnmpConstants.ErrWrongEncoding:
                    return "wrongEncoding";
                case SnmpConstants.ErrWrongValue:
                    return "wrongValue";
                case SnmpConstants.ErrNoCreation:
                    return "noCreation";
                case SnmpConstants.ErrInconsistentValue:
                    return "inconsistentValue";
                case SnmpConstants.ErrResourceUnavailable:
                    return "resourceUnavailable";
                case SnmpConstants.ErrCommitFailed:
                    return "commitFailed";
                case SnmpConstants.ErrUndoFailed:
                    return "undoFailed";
                case SnmpConstants.ErrAuthorizationError:
                    return "authorizationError";
                case SnmpConstants.ErrNotWritable:
                    return "notWritable";
                case SnmpConstants.ErrInconsistentName:
                    return "inconsistentName";
                default:
                    return string.Format("Unknown error ({0})", errorCode);
            }
        }

        /// <summary>
        /// Private constructor.
        /// </summary>
        private SnmpError()
        {
        }
    }
}
