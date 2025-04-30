using Microsoft.Extensions.Logging;
using Portalum.Zvt.Models;
using Portalum.Zvt.Repositories;
using Portalum.Zvt.Responses;
using System;
using System.Text;

namespace Portalum.Zvt.Parsers
{
    /// <summary>
    /// IntermediateStatusInformationParser
    /// </summary>
    public class IntermediateStatusInformationParser : IIntermediateStatusInformationParser
    {
        private readonly ILogger _logger;
        private readonly Encoding _encoding;
        private readonly IIntermediateStatusRepository _intermediateStatusRepository;
        private readonly BmpParser _bmpParser;
        private readonly TlvParser _tlvParser;

        private readonly StringBuilder _tlvTextContent;

        /// <summary>
        /// IntermediateStatusInformationParser
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="encoding"></param>
        /// <param name="intermediateStatusRepository"></param>
        /// <param name="errorMessageRepository"></param>
        public IntermediateStatusInformationParser(
            ILogger logger,
            Encoding encoding,
            IIntermediateStatusRepository intermediateStatusRepository,
            IErrorMessageRepository errorMessageRepository)
        {
            this._logger = logger;
            this._encoding = encoding;
            this._intermediateStatusRepository = intermediateStatusRepository;

            var tlvInfos = new TlvInfo[]
            {
                new TlvInfo { Tag = "24", Description = "Display-texts", TryProcess = this.CleanupTextBuffer },
                new TlvInfo { Tag = "07", Description = "Text-Lines", TryProcess = this.AddTextLine },
            };

            this._tlvParser = new TlvParser(logger, tlvInfos);
            this._bmpParser = new BmpParser(logger, encoding, errorMessageRepository, this._tlvParser);

            this._tlvTextContent = new StringBuilder();
        }

        /// <inheritdoc />
        public string GetMessage(Span<byte> data)
        {
            if (data.Length == 0)
            {
                return null;
            }

            var id = data[0];
            //var timeout = data[1];

            string message = this.TryParseTlvMessage(data);
            if (!string.IsNullOrEmpty(message))
            {
                return message;
            }

            message = this._intermediateStatusRepository.GetMessage(id);
            if (!string.IsNullOrEmpty(message))
            {
                return message;
            }

            this._logger.LogError($"{nameof(GetMessage)} - No message available for ID {id:X2} (neither TLV nor repository).");
            return null;
        }

        /// <summary>
        /// TryParseTlvMessage
        /// </summary>
        private string TryParseTlvMessage(Span<byte> data)
        {
            if (data.Length <= 3)
            {
                return null; // Not enough data for TLV
            }

            var tlvData = data.Slice(2); 

            if (!this._bmpParser.Parse(tlvData, null))
            {
                 this._logger.LogWarning($"{nameof(TryParseTlvMessage)} - Failed to parse potential TLV data.");
                 return null;
            }

            var tlvMessage = this._tlvTextContent.ToString();
            if (string.IsNullOrEmpty(tlvMessage))
            {
                this._logger.LogWarning($"{nameof(TryParseTlvMessage)} - Potential TLV data parsed but content is empty.");
                return null;
            }

            return tlvMessage;
        }

        private bool CleanupTextBuffer(byte[] data, IResponse response)
        {
            this._tlvTextContent.Clear();

            return true;
        }

        private bool AddTextLine(byte[] data, IResponse response)
        {
            var textBlock = this._encoding.GetString(data);
            this._tlvTextContent.AppendLine(textBlock);

            return true;
        }
    }
}
