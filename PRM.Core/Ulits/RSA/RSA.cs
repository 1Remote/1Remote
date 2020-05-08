using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace com.github.xiangyuecn.rsacsharp {
	/// <summary>
	/// RSA操作类
	/// GitHub: https://github.com/xiangyuecn/RSA-csharp
	/// </summary>
	public class RSA {
		/// <summary>
		/// 导出XML格式密钥对，如果convertToPublic含私钥的RSA将只返回公钥，仅含公钥的RSA不受影响
		/// </summary>
		public string ToXML(bool convertToPublic = false) {
			return rsa.ToXmlString(!rsa.PublicOnly && !convertToPublic);
		}
		/// <summary>
		/// 导出PEM PKCS#1格式密钥对，如果convertToPublic含私钥的RSA将只返回公钥，仅含公钥的RSA不受影响
		/// </summary>
		public string ToPEM_PKCS1(bool convertToPublic = false) {
			return new RSA_PEM(rsa).ToPEM(convertToPublic, false);
		}
		/// <summary>
		/// 导出PEM PKCS#8格式密钥对，如果convertToPublic含私钥的RSA将只返回公钥，仅含公钥的RSA不受影响
		/// </summary>
		public string ToPEM_PKCS8(bool convertToPublic = false) {
			return new RSA_PEM(rsa).ToPEM(convertToPublic, true);
		}
		/// <summary>
		/// 将密钥对导出成PEM对象，如果convertToPublic含私钥的RSA将只返回公钥，仅含公钥的RSA不受影响
		/// </summary>
		public RSA_PEM ToPEM(bool convertToPublic = false) {
			return new RSA_PEM(rsa, convertToPublic);
		}




		/// <summary>
		/// 加密字符串（utf-8），出错抛异常
		/// </summary>
		public string Encode(string str) {
			return Convert.ToBase64String(Encode(Encoding.UTF8.GetBytes(str)));
		}
		/// <summary>
		/// 加密数据，出错抛异常
		/// </summary>
		public byte[] Encode(byte[] data) {
			int blockLen = rsa.KeySize / 8 - 11;
			if (data.Length <= blockLen) {
				return rsa.Encrypt(data, false);
			}

			using (var dataStream = new MemoryStream(data))
			using (var enStream = new MemoryStream()) {
				Byte[] buffer = new Byte[blockLen];
				int len = dataStream.Read(buffer, 0, blockLen);

				while (len > 0) {
					Byte[] block = new Byte[len];
					Array.Copy(buffer, 0, block, 0, len);

					Byte[] enBlock = rsa.Encrypt(block, false);
					enStream.Write(enBlock, 0, enBlock.Length);

					len = dataStream.Read(buffer, 0, blockLen);
				}

				return enStream.ToArray();
			}
		}
		/// <summary>
		/// 解密字符串（utf-8），解密异常返回null
		/// </summary>
		public string DecodeOrNull(string str) {
			if (String.IsNullOrEmpty(str)) {
				return null;
			}
			byte[] byts = null;
			try { byts = Convert.FromBase64String(str); } catch { }
			if (byts == null) {
				return null;
			}
			var val = DecodeOrNull(byts);
			if (val == null) {
				return null;
			}
			return Encoding.UTF8.GetString(val);
		}
		/// <summary>
		/// 解密数据，解密异常返回null
		/// </summary>
		public byte[] DecodeOrNull(byte[] data) {
			try {
				int blockLen = rsa.KeySize / 8;
				if (data.Length <= blockLen) {
					return rsa.Decrypt(data, false);
				}

				using (var dataStream = new MemoryStream(data))
				using (var deStream = new MemoryStream()) {
					Byte[] buffer = new Byte[blockLen];
					int len = dataStream.Read(buffer, 0, blockLen);

					while (len > 0) {
						Byte[] block = new Byte[len];
						Array.Copy(buffer, 0, block, 0, len);

						Byte[] deBlock = rsa.Decrypt(block, false);
						deStream.Write(deBlock, 0, deBlock.Length);

						len = dataStream.Read(buffer, 0, blockLen);
					}

					return deStream.ToArray();
				}
			} catch {
				return null;
			}
		}
		/// <summary>
		/// 对str进行签名，并指定hash算法（如：SHA256）
		/// </summary>
		public string Sign(string hash, string str) {
			return Convert.ToBase64String(Sign(hash, Encoding.UTF8.GetBytes(str)));
		}
		/// <summary>
		/// 对data进行签名，并指定hash算法（如：SHA256）
		/// </summary>
		public byte[] Sign(string hash, byte[] data) {
			return rsa.SignData(data, hash);
		}
		/// <summary>
		/// 验证字符串str的签名是否是sgin，并指定hash算法（如：SHA256）
		/// </summary>
		public bool Verify(string hash, string sgin, string str) {
			byte[] byts = null;
			try { byts = Convert.FromBase64String(sgin); } catch { }
			if (byts == null) {
				return false;
			}
			return Verify(hash, byts, Encoding.UTF8.GetBytes(str));
		}
		/// <summary>
		/// 验证data的签名是否是sgin，并指定hash算法（如：SHA256）
		/// </summary>
		public bool Verify(string hash, byte[] sgin, byte[] data) {
			try {
				return rsa.VerifyData(data, hash, sgin);
			} catch {
				return false;
			}
		}




		private RSACryptoServiceProvider rsa;
		/// <summary>
		/// 最底层的RSACryptoServiceProvider对象
		/// </summary>
		public RSACryptoServiceProvider RSAObject {
			get {
				return rsa;
			}
		}

		/// <summary>
		/// 密钥位数
		/// </summary>
		public int KeySize {
			get {
				return rsa.KeySize;
			}
		}
		/// <summary>
		/// 是否包含私钥
		/// </summary>
		public bool HasPrivate {
			get {
				return !rsa.PublicOnly;
			}
		}

		/// <summary>
		/// 用指定密钥大小创建一个新的RSA，出错抛异常
		/// </summary>
		public RSA(int keySize) {
			var rsaParams = new CspParameters();
			rsaParams.Flags = CspProviderFlags.UseMachineKeyStore;
			rsa = new RSACryptoServiceProvider(keySize, rsaParams);
		}
		/// <summary>
		/// 通过指定的密钥，创建一个RSA，xml内可以只包含一个公钥或私钥，或都包含，出错抛异常
		/// </summary>
		public RSA(string xml) {
			var rsaParams = new CspParameters();
			rsaParams.Flags = CspProviderFlags.UseMachineKeyStore;
			rsa = new RSACryptoServiceProvider(rsaParams);

			rsa.FromXmlString(xml);
		}
		/// <summary>
		/// 通过一个pem文件创建RSA，pem为公钥或私钥，出错抛异常
		/// </summary>
		public RSA(string pem, bool noop) {
			rsa = RSA_PEM.FromPEM(pem).GetRSA();
		}
		/// <summary>
		/// 通过一个pem对象创建RSA，pem为公钥或私钥，出错抛异常
		/// </summary>
		public RSA(RSA_PEM pem) {
			rsa = pem.GetRSA();
		}
	}
}
