using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Client;
using Medallion.OData.Parser;
using NUnit.Framework;

namespace Medallion.OData.Tests
{
    public class ODataParsingTest
    {
        #region ---- Expression language parsing ----
        // from http://www.odata.org/documentation/odata-v2-documentation/uri-conventions/#4_Query_String_Options
        [Test]
        public void TestParseExpressionLanguageAddressCityEqRedmond() { this.TestParseExpressionLanguage("Address/City eq 'Redmond'"); }
        [Test]
        public void TestParseExpressionLanguageAddressCityNeRedmond() { this.TestParseExpressionLanguage("Address/City ne 'Redmond'"); }
        [Test]
        public void TestParseExpressionLanguagePriceGt() { this.TestParseExpressionLanguage("Price gt 20"); }
        [Test]
        public void TestParseExpressionLanguagePriceGe() { this.TestParseExpressionLanguage("Price ge 10"); }
        [Test]
        public void TestParseExpressionLanguagePriceLt() { this.TestParseExpressionLanguage("Price lt 20"); }
        [Test]
        public void TestParseExpressionLanguagePriceLeAndPriceGt() { this.TestParseExpressionLanguage("Price le 200 and Price gt 3.5"); }
        [Test]
        public void TestParseExpressionLanguagePriceLeOrPriceGt() { this.TestParseExpressionLanguage("Price le 3.5 or Price gt 200"); }
        [Test]
        public void TestParseExpressionLanguageNotEndswithDescriptionMilk() { this.TestParseExpressionLanguage("not endswith(Description, 'milk')"); }
        [Test]
        public void TestParseExpressionLanguagePriceAddGt() { this.TestParseExpressionLanguage("Price add 5 gt 10"); }
        [Test]
        public void TestParseExpressionLanguagePriceSubGt() { this.TestParseExpressionLanguage("Price sub 5 gt 10"); }
        [Test]
        public void TestParseExpressionLanguagePriceMulGt() { this.TestParseExpressionLanguage("Price mul 2 gt 2000"); }
        [Test]
        public void TestParseExpressionLanguagePriceDivGt() { this.TestParseExpressionLanguage("Price div 2 gt 4"); }
        [Test]
        public void TestParseExpressionLanguagePriceModEq() { this.TestParseExpressionLanguage("Price mod 2 eq 0"); }
        [Test]
        public void TestParseExpressionLanguagePriceSubGe() { this.TestParseExpressionLanguage("Price sub 5 ge 10"); }
        [Test]
        public void TestParseExpressionLanguageBoolOrNotBoolAndNe() { this.TestParseExpressionLanguage("(Bool or not Bool) and 1 ne 2"); }
        [Test]
        public void TestParseExpressionLanguageSubstringofAlfredsCompanyNameEqTrue() { this.TestParseExpressionLanguage("substringof('Alfreds', CompanyName) eq true"); }
        [Test]
        public void TestParseExpressionLanguageEndswithCompanyNameFutterkisteEqTrue() { this.TestParseExpressionLanguage("endswith(CompanyName, 'Futterkiste') eq true"); }
        [Test]
        public void TestParseExpressionLanguageStartswithCompanyNameAlfrEqTrue() { this.TestParseExpressionLanguage("startswith(CompanyName, 'Alfr') eq true"); }
        [Test]
        public void TestParseExpressionLanguageLengthCompanyNameEq() { this.TestParseExpressionLanguage("length(CompanyName) eq 19"); }
        [Test]
        public void TestParseExpressionLanguageIndexofCompanyNameLfredsEq() { this.TestParseExpressionLanguage("indexof(CompanyName, 'lfreds') eq 1"); }
        [Test]
        public void TestParseExpressionLanguageReplaceCompanyNameEqAlfredsFutterkiste() { this.TestParseExpressionLanguage("replace(CompanyName, ' ', '') eq 'AlfredsFutterkiste'"); }
        [Test]
        public void TestParseExpressionLanguageSubstringCompanyNameEqLfredsFutterkiste() { this.TestParseExpressionLanguage("substring(CompanyName, 1) eq 'lfreds Futterkiste'"); }
        [Test]
        public void TestParseExpressionLanguageSubstringCompanyNameEqLf() { this.TestParseExpressionLanguage("substring(CompanyName, 1, 2) eq 'lf'"); }
        [Test]
        public void TestParseExpressionLanguageTolowerCompanyNameEqAlfredsFutterkiste() { this.TestParseExpressionLanguage("tolower(CompanyName) eq 'alfreds futterkiste'"); }
        [Test]
        public void TestParseExpressionLanguageToupperCompanyNameEqALFREDSFUTTERKISTE() { this.TestParseExpressionLanguage("toupper(CompanyName) eq 'ALFREDS FUTTERKISTE'"); }
        [Test]
        public void TestParseExpressionLanguageTrimCompanyNameEqAlfredsFutterkiste() { this.TestParseExpressionLanguage("trim(CompanyName) eq 'Alfreds Futterkiste'"); }
        [Test]
        public void TestParseExpressionLanguageConcatConcatCityCountryEqBerlinGermany() { this.TestParseExpressionLanguage("concat(concat(City, ', '), Country) eq 'Berlin, Germany'"); }
        [Test]
        public void TestParseExpressionLanguageDayBirthDateEq() { this.TestParseExpressionLanguage("day(BirthDate) eq 8"); }
        [Test]
        public void TestParseExpressionLanguageHourBirthDateEq() { this.TestParseExpressionLanguage("hour(BirthDate) eq 0"); }
        [Test]
        public void TestParseExpressionLanguageMinuteBirthDateEq() { this.TestParseExpressionLanguage("minute(BirthDate) eq 0"); }
        [Test]
        public void TestParseExpressionLanguageMonthBirthDateEq() { this.TestParseExpressionLanguage("month(BirthDate) eq 12"); }
        [Test]
        public void TestParseExpressionLanguageSecondBirthDateEq() { this.TestParseExpressionLanguage("second(BirthDate) eq 0"); }
        [Test]
        public void TestParseExpressionLanguageYearBirthDateEq() { this.TestParseExpressionLanguage("year(BirthDate) eq 1948"); }
        [Test]
        public void TestParseExpressionLanguageRoundFreightEq() { this.TestParseExpressionLanguage("round(Freight) eq 32"); }
        [Test]
        public void TestParseExpressionLanguageRoundFreightMEq() { this.TestParseExpressionLanguage("round(FreightM) eq 32"); }
        [Test]
        public void TestParseExpressionLanguageFloorFreightEq() { this.TestParseExpressionLanguage("floor(Freight) eq 32"); }
        [Test]
        public void TestParseExpressionLanguageFloorFreightMEq() { this.TestParseExpressionLanguage("floor(FreightM) eq 32"); }
        [Test]
        public void TestParseExpressionLanguageCeilingFreightEq() { this.TestParseExpressionLanguage("ceiling(Freight) eq 33"); }
        [Test]
        public void TestParseExpressionLanguageCeilingFreightMEq() { this.TestParseExpressionLanguage("ceiling(FreightM) eq 33"); }
        [Test]
        public void TestParseExpressionLanguageIsofMedallionODataTestsODataParsingTestB() { this.TestParseExpressionLanguage("isof('Medallion.OData.Tests.ODataParsingTest+B')"); }
        [Test]
        public void TestParseExpressionLanguageIsofShipCountryEdmString() { this.TestParseExpressionLanguage("isof(ShipCountry, 'Edm.String')"); }

        // from http://www.odata.org/documentation/overview/#AbstractDataModel
        [Test]
        public void TestParseExpressionLanguageNull() { this.TestParseExpressionLanguage("null"); }
        [Test]
        public void TestParseExpressionLanguageTrue() { this.TestParseExpressionLanguage("true"); }
        [Test]
        public void TestParseExpressionLanguageFalse() { this.TestParseExpressionLanguage("false"); }
        [Test]
        public void TestParseExpressionLanguageDatetimeIso() { this.TestParseExpressionLanguage("datetime'2000-12-12T12:00'"); }
        [Test]
        public void TestParseExpressionLanguageM() { this.TestParseExpressionLanguage("2.345M"); }
        [Test]
        public void TestParseExpressionLanguageE() { this.TestParseExpressionLanguage("1E+30"); }
        [Test]
        public void TestParseExpressionLanguageFloat() { this.TestParseExpressionLanguage("2.029"); }
        [Test]
        public void TestParseExpressionLanguageFloat2() { this.TestParseExpressionLanguage("2.1"); }
        [Test]
        public void TestParseExpressionLanguageF() { this.TestParseExpressionLanguage("2.0f"); }
        [Test]
        public void TestParseExpressionLanguageGuidAaaaBbbbCcccDdddeeeeffff() { this.TestParseExpressionLanguage("guid'12345678-aaaa-bbbb-cccc-ddddeeeeffff'"); }
        [Test]
        public void TestParseExpressionLanguageInt() { this.TestParseExpressionLanguage("32"); }
        [Test]
        public void TestParseExpressionLanguageNegativeInt() { this.TestParseExpressionLanguage("-32"); }
        [Test]
        public void TestParseExpressionLanguageL() { this.TestParseExpressionLanguage("64L"); }
        [Test]
        public void TestParseExpressionLanguageNegativeL() { this.TestParseExpressionLanguage("-64L"); }
        [Test]
        public void TestParseExpressionLanguageHelloOData() { this.TestParseExpressionLanguage("'Hello OData'"); }
        //[TestCase("X'23AB", Ignore = true)] // TODO FUTURE not implemented
        //[TestCase("binary'23ABFF'", Ignore = true)] // TODO FUTURE not implemented
        //[TestCase("FF", Ignore = true)] // TODO FUTURE not implemented
        //[TestCase("13:20:00", Ignore = true)] // TODO VNEXT time not implemented
        //[TestCase("datetimeoffset'2002-10-10T17:00:00Z'", Ignore = true)] // TODO VNEXT datetimeoffet not implemente
        
        // my test cases
        [Test]
        public void TestParseExpressionLanguageTextNeNull() { this.TestParseExpressionLanguage("Text ne null"); }
        [Test]
        public void TestParseExpressionLanguageThisStringContainsQuotes() { this.TestParseExpressionLanguage("'this string contains ''quotes'''"); }
        [Test]
        public void TestParseExpressionLanguageDatetimeT() { this.TestParseExpressionLanguage("datetime'2010-01-01T00:00:01'"); }
        [Test]
        public void TestParseExpressionLanguageDatetimeT2() { this.TestParseExpressionLanguage("datetime'2010-01-01T00:00:00.0100000'"); }
        [Test]
        public void TestParseExpressionLanguageCastIntEdmDouble() { this.TestParseExpressionLanguage("cast(Int, 'Edm.Double')"); }
        [Test]
        public void TestParseExpressionLanguageSubAddMul() { this.TestParseExpressionLanguage("1 sub 2 add 3 mul 4"); }
        [Test]
        public void TestParseUnaryPrecedence()
        {
            this.TestParseExpressionLanguage("not Bool and Bool");
            this.TestParseExpressionLanguage("not(Bool and not Bool)");
        }
        [Test]
        public void TestParseMultipleNegation()
        {
            this.TestParseExpressionLanguage("not not not Bool");
        }

        [Test]
        public void TestParseIntegerOverflow()
        {
            UnitTestHelpers.AssertThrows<OverflowException>(() => this.TestParseExpressionLanguage("Int lt 2147483648"));
        }

        [Test]
        public void TestParseDynamic()
        {
            this.TestParseExpressionLanguage("1 sub Price gt Freight");
            this.TestParseExpressionLanguage("null eq Text");
            this.TestParseExpressionLanguage("substring('abc', 0, Int add Int)");
            this.TestParseExpressionLanguage("FreightM add FreightM add cast(1, 'Edm.Decimal')");
        }

        private void TestParseExpressionLanguage(string input, Type type = null)
        {
            var parser = new ODataExpressionLanguageParser(type ?? typeof(A), input);
            var parsed = parser.Parse();
            parsed.ToString().ShouldEqual(input);

            var dynamicParser = new ODataExpressionLanguageParser(typeof(ODataEntity), input);
            var dynamicParsed = dynamicParser.Parse();
            parsed.ToString().ShouldEqual(input, "dynamic parse");
        }
        #endregion

        #region ---- Sort key parsing ----
        // test from http://www.odata.org/documentation/odata-v2-documentation/uri-conventions/#4_Query_String_Options
        [Test]
        public void TestParseSortKeysInt() { this.TestParseSortKeys("Int", null); }
        [Test]
        public void TestParseSortKeysIntAsc() { this.TestParseSortKeys("Int asc", "Int"); }
        [Test]
        public void TestParseSortKeysIntAddress() { this.TestParseSortKeys("Int,Address/City desc", null); }
        public void TestParseSortKeys(string input, string expected = null)
        {
            var parser = new ODataExpressionLanguageParser(typeof(A), input);
            var parsed = parser.ParseSortKeyList();
            parsed.ToDelimitedString().ShouldEqual(expected ?? input);

            var dynamicParser = new ODataExpressionLanguageParser(typeof(ODataEntity), input);
            var dynamicParsed = dynamicParser.ParseSortKeyList();
            dynamicParsed.ToDelimitedString().ShouldEqual(expected ?? input);
        }
        #endregion

        #region ---- Select parsing ----
        [Test]
        public void TestParseSelectStar() { this.TestParseSelect("*"); }
        [Test]
        public void TestParseSelectIntBool() { this.TestParseSelect("Int,Bool"); }
        [Test]
        public void TestParseSelectInner() { this.TestParseSelect("Inner/*,*"); }
        [Test]
        public void TestParseSelectBirthDate() { this.TestParseSelect("BirthDate,Inner/Int,Inner/Bool"); }

        private void TestParseSelect(string input)
        {
            var parser = new ODataExpressionLanguageParser(typeof(A), input);
            var parsed = parser.ParseSelectColumnList();
            parsed.ToDelimitedString().ShouldEqual(input);

            var dynamicParser = new ODataExpressionLanguageParser(typeof(ODataEntity), input);
            var dynamicParsed = dynamicParser.ParseSelectColumnList();
            dynamicParsed.ToDelimitedString().ShouldEqual(input);
        }
        #endregion

        #region ---- Query parsing ----
        [Test]
        public void TestParseQueryTopSkip() { this.TestParseQuery("?$top=1&$skip=5"); }
        [Test]
        public void TestParseQueryFilter() { this.TestParseQuery("?$filter=1+ne+2+and+substringof(Address%2FCity%2C+%27blah%27)"); }
        [Test]
        public void TestParseQuerySelect() { this.TestParseQuery("?$select=Freight%2CBool"); }

        private void TestParseQuery(string query)
        {
            var parsed = ODataQueryParser.Parse(typeof(A), query);
            parsed.ToString().ShouldEqual(query);

            var dynamicParsed = ODataQueryParser.Parse(typeof(ODataEntity), query);
            dynamicParsed.ToString().ShouldEqual(query);
        }

        [Test]
        public void TestParseQueryWithEmpty()
        {
            var parameters = new[] { "top", "skip", "filter", "select", "orderby", "format", "inlinecount" };

            // empty is allowed (see http://services.odata.org/v3/odata/odata.svc/Categories?$filter=&$format=json)
            foreach (var parameter in parameters)
            {
                UnitTestHelpers.AssertDoesNotThrow(() => ODataQueryParser.Parse(typeof(A), string.Format("?${0}=", parameter)));
            }

            // whitespace is not allowed (see http://services.odata.org/v3/odata/odata.svc/Categories?$orderby=%20&$format=json)
            foreach (var parameter in parameters)
            {
                UnitTestHelpers.AssertThrows<ODataParseException>(() => ODataQueryParser.Parse(typeof(A), string.Format("?${0}= ", parameter)));
            }
        }
        #endregion

        #region ---- Lex tests ----
        [Test]
        public void TestOperatorSpacing() 
        {
           this.TestLex("aeq(eq)", ODataTokenKind.Identifier, ODataTokenKind.LeftParen, ODataTokenKind.Eq, ODataTokenKind.RightParen);
        }

        [Test]
        public void TestOperatorInPath()
        {
            // TODO FUTURE debatable whether this should match eq as an identifier. Should
            // we use lookbehind to prevent this?
            this.TestLex("a/eq", ODataTokenKind.Identifier, ODataTokenKind.Slash, ODataTokenKind.Eq);
        }

        private void TestLex(string text, params ODataTokenKind[] expected)
        {
            var list = ODataExpressionLanguageTokenizer.Tokenize(text).Select(t => t.Kind).ToList();
            list[list.Count - 1].ShouldEqual(ODataTokenKind.Eof);
            list.GetRange(0, list.Count - 1).CollectionShouldEqual(expected, orderMatters: true, message: list.ToDelimitedString(", "));
        }
        #endregion

        #region ---- Sample classes ----
        private class A
        {
            public int Int { get; set; }
            public bool Bool { get; set; }
            public string Text { get; set; }
            public double Price { get; set; }
            public string Description { get; set; }
            public string CompanyName { get; set; }
            public string City { get; set; }
            public string Country { get; set; }
            public DateTime? BirthDate { get; set; }
            public double? Freight { get; set; }
            public decimal FreightM { get; set; }
            public string ShipCountry { get; set; }
            public Address Address { get; set; }
            public A Inner { get; set; }
        }

        private class B : A
        {
        }

        private class Address
        {
            public string City { get; set; }
        }
        #endregion
    }
}
