using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models
{
    public struct TransmissionSegment
    {
        // Non-owning - belongs to a pool!
        private float[] Audio { get; set; }
        private int AudioLength { get; set; }

        public Span<float> AudioSpan => Audio.AsSpan(0, AudioLength);
        public bool HasEncryption { get; private set; }
        public bool Decryptable { get; private set; }
        public string OriginalClientGuid { get; private set; }
        public string ClientGuid { get; private set; }
        public bool IsSecondary {  get; private set; }
        public Modulation Modulation { get; private set; }
        public double ReceivingPower { get; private set; }
        public bool NoAudioEffects { get; private set; }

        public TransmissionSegment(DeJitteredTransmission transmission, float[] audio, int audioLength)
        {
            Audio = audio;
            AudioLength = audioLength;
            HasEncryption = transmission.Encryption > 0;
            Decryptable = transmission.Decryptable;
            OriginalClientGuid = transmission.OriginalClientGuid;
            IsSecondary = transmission.IsSecondary;
            Modulation = transmission.Modulation;
            ClientGuid = transmission.Guid;
            ReceivingPower = transmission.ReceivingPower;
            NoAudioEffects = transmission.NoAudioEffects;
        }

        public void Return(ArrayPool<float> floatPool)
        {
            floatPool.Return(Audio);
            Audio = null;
            AudioLength = 0;
        }
    }
}
