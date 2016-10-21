
namespace Subspace
{
    /// <summary>
    /// XML-RPC templates to send to OpenSubtitles.
    /// </summary>
    /// <remarks>
    /// Simply replace the parameters surrounded by '%',
    /// by their value.
    /// </remarks>
    public static class Templates
    {
        /// <summary>
        /// PARAMS: LANG, UA
        /// </summary>
        public const string LOG_IN = @"
<?xml version=""1.0""?>
<methodCall>
  <methodName>LogIn</methodName>
  <params>
    <param>
      <value>
        <string />
      </value>
    </param>
    <param>
      <value>
        <string />
      </value>
    </param>
    <param>
      <value>
        <string>%LANG%</string>
      </value>
    </param>
    <param>
      <value>
        <string>%UA%</string>
      </value>
    </param>
  </params>
</methodCall>
";

        /// <summary>
        /// PARAMS: TOKEN
        /// </summary>
        public const string LOG_OUT = @"
<?xml version=""1.0""?>
<methodCall>
  <methodName>LogOut</methodName>
  <params>
    <param>
      <value>
        <string>%TOKEN%</string>
      </value>
    </param>
  </params>
</methodCall>
";

        /// <summary>
        /// PARAMS: TOKEN, LANG, HASH, SIZE
        /// </summary>
        public const string SEARCH_HASH = @"
<?xml version=""1.0""?>
<methodCall>
  <methodName>SearchSubtitles</methodName>
  <params>
    <param>
      <value>
        <string>%TOKEN%</string>
      </value>
    </param>
    <param>
      <value>
        <array>
          <data>
            <value>
              <struct>
                <member>
                  <name>sublanguageid</name>
                  <value>
                    <string>%LANG%</string>
                  </value>
                </member>
                <member>
                  <name>moviehash</name>
                  <value>
                    <string>%HASH%</string>
                  </value>
                </member>
                <member>
                  <name>moviebytesize</name>
                  <value>
                    <string>%SIZE%</string>
                  </value>
                </member>
              </struct>
            </value>
          </data>
        </array>
      </value>
    </param>
  </params>
</methodCall>
";

        /// <summary>
        /// PARAMS: TOKEN, LANG, QUERY
        /// </summary>
        public const string SEARCH_MOVIE = @"
<?xml version=""1.0""?>
<methodCall>
  <methodName>SearchSubtitles</methodName>
  <params>
    <param>
      <value>
        <string>%TOKEN%</string>
      </value>
    </param>
    <param>
      <value>
        <array>
          <data>
            <value>
              <struct>
                <member>
                  <name>sublanguageid</name>
                  <value>
                    <string>%LANG%</string>
                  </value>
                </member>
                <member>
                  <name>query</name>
                  <value>
                    <string>%QUERY%</string>
                  </value>
                </member>
              </struct>
            </value>
          </data>
        </array>
      </value>
    </param>
  </params>
</methodCall>
";

        /// <summary>
        /// PARAMS: TOKEN, LANG, QUERY, SEASON, EPISODE
        /// </summary>
        public const string SEARCH_TVSHOW = @"
<?xml version=""1.0""?>
<methodCall>
  <methodName>SearchSubtitles</methodName>
  <params>
    <param>
      <value>
        <string>%TOKEN%</string>
      </value>
    </param>
    <param>
      <value>
        <array>
          <data>
            <value>
              <struct>
                <member>
                  <name>sublanguageid</name>
                  <value>
                    <string>%LANG%</string>
                  </value>
                </member>
                <member>
                  <name>query</name>
                  <value>
                    <string>%QUERY%</string>
                  </value>
                </member>
                <member>
                  <name>season</name>
                  <value>
                    <string>%SEASON%</string>
                  </value>
                </member>
                <member>
                  <name>episode</name>
                  <value>
                    <string>%EPISODE%</string>
                  </value>
                </member>
              </struct>
            </value>
          </data>
        </array>
      </value>
    </param>
  </params>
</methodCall>
";

        /// <summary>
        /// PARAMS: TOKEN, LANG, TAG
        /// </summary>
        public const string SEARCH_TAG = @"
<?xml version=""1.0""?>
<methodCall>
  <methodName>SearchSubtitles</methodName>
  <params>
    <param>
      <value>
        <string>%TOKEN%</string>
      </value>
    </param>
    <param>
      <value>
        <array>
          <data>
            <value>
              <struct>
                <member>
                  <name>sublanguageid</name>
                  <value>
                    <string>%LANG%</string>
                  </value>
                </member>
                <member>
                  <name>tag</name>
                  <value>
                    <string>%TAG%</string>
                  </value>
                </member>
              </struct>
            </value>
          </data>
        </array>
      </value>
    </param>
  </params>
</methodCall>
";
    }
}
