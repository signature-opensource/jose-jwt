﻿namespace Jose.Jwe
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Internal class used to represent the data withing a JWE
    /// Note - could have come in as compant, flattened json or general json.
    /// </summary>
    internal class ParsedJwe
    {
        internal static ParsedJwe Parse(string jwe, JwtSettings settings)
        {
            jwe = jwe.Trim();
            bool modeIsJson = jwe.StartsWith("{", StringComparison.Ordinal);

            if (modeIsJson)
            {
                return ParseJson(jwe, settings);
            }
            else
            {
                return ParseCompact(jwe);
            }
        }

        internal byte[] ProtectedHeaderBytes { get; }
        internal IDictionary<string, object> UnprotectedHeader { get; }
        internal List<(byte[] EncryptedCek, IDictionary<string, object> Header)> Recipients { get; }
        internal byte[] Aad { get; }
        internal byte[] Iv { get; }
        internal byte[] Ciphertext { get; }
        internal byte[] AuthTag { get; }
        internal SerializationMode Encoding { get; }

        private ParsedJwe(
            byte[] protectedHeaderBytes,
            IDictionary<string, object> unprotectedHeader,
            List<(byte[] EncryptedCek, IDictionary<string, object> Header)> recipients,
            byte[] aad,
            byte[] iv,
            byte[] ciphertext,
            byte[] authTag,
            SerializationMode encoding)
        {
            ProtectedHeaderBytes = protectedHeaderBytes;
            UnprotectedHeader = unprotectedHeader;
            Recipients = recipients;
            Aad = aad;
            Iv = iv;
            Ciphertext = ciphertext;
            AuthTag = authTag;
            Encoding = encoding;
        }

        private static ParsedJwe ParseCompact(string jwe)
        {
            var parts = Compact.Iterate(jwe);

            var protectedHeaderBytes = parts.Next();
            byte[] encryptedCek = parts.Next();
            var iv = parts.Next();
            var ciphertext = parts.Next();
            var authTag = parts.Next();

            var recipients = new List<(byte[] EncryptedCek, IDictionary<string, object> Header)>
                {
                    ((EncryptedCek: encryptedCek, Header: new Dictionary<string, object>())),
                };

            return new ParsedJwe(
                protectedHeaderBytes: protectedHeaderBytes,
                unprotectedHeader: null,
                aad: null,
                recipients: recipients,
                iv: iv,
                ciphertext: ciphertext,
                authTag: authTag,
                encoding: SerializationMode.Compact);
        }

        private static ParsedJwe ParseJson(string jwe, JwtSettings settings)
        {
            // TODO - do we want the entire object deserialized using the custom JsonMapper?
            var jweJson = settings.JsonMapper.Parse<JweJson>(jwe);

            var recipients = new List<(byte[] EncryptedCek, IDictionary<string, object> Header)>();
            if (jweJson.recipients?.Count() > 0)
            {
                foreach (var recipient in jweJson.recipients)
                {
                    byte[] encryptedCek = Base64Url.Decode(recipient.encrypted_key);
                    recipients.Add((EncryptedCek: encryptedCek, Header: recipient.header));
                }
            }
            else
            {
                byte[] encryptedCek = Base64Url.Decode(jweJson.encrypted_key);
                recipients.Add((EncryptedCek: encryptedCek, Header: jweJson.header));
            }

            return new ParsedJwe(
                protectedHeaderBytes: jweJson.@protected == null ? new byte[0] : Base64Url.Decode(jweJson.@protected),
                unprotectedHeader: jweJson.unprotected,
                aad: jweJson.aad == null ? null : Base64Url.Decode(jweJson.aad),
                recipients: recipients,
                iv: Base64Url.Decode(jweJson.iv),
                ciphertext: Base64Url.Decode(jweJson.ciphertext),
                authTag: Base64Url.Decode(jweJson.tag),
                encoding: SerializationMode.Json);
        }
    }
}