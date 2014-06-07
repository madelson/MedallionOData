using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Medallion.OData.Tests
{
	[TestClass]
	public class ODataParsingTest
    {
        #region ---- Expression language parsing ----
        // from http://www.odata.org/documentation/odata-v2-documentation/uri-conventions/#4_Query_String_Options
        [TestMethod]
        public void TestParseExpressionLanguageAddressCityEqRedmond() { this.TestParseExpressionLanguage("Address/City eq 'Redmond'"); }
        [TestMethod]
        public void TestParseExpressionLanguageAddressCityNeRedmond() { this.TestParseExpressionLanguage("Address/City ne 'Redmond'"); }
        [TestMethod]
        public void TestParseExpressionLanguagePriceGt() { this.TestParseExpressionLanguage("Price gt 20"); }
        [TestMethod]
        public void TestParseExpressionLanguagePriceGe() { this.TestParseExpressionLanguage("Price ge 10"); }
        [TestMethod]
        public void TestParseExpressionLanguagePriceLt() { this.TestParseExpressionLanguage("Price lt 20"); }
        [TestMethod]
        public void TestParseExpressionLanguagePriceLeAndPriceGt() { this.TestParseExpressionLanguage("Price le 200 and Price gt 3.5"); }
        [TestMethod]
        public void TestParseExpressionLanguagePriceLeOrPriceGt() { this.TestParseExpressionLanguage("Price le 3.5 or Price gt 200"); }
        [TestMethod]
        public void TestParseExpressionLanguageNotEndswithDescriptionMilk() { this.TestParseExpressionLanguage("not endswith(Description, 'milk')"); }
        [TestMethod]
        public void TestParseExpressionLanguagePriceAddGt() { this.TestParseExpressionLanguage("Price add 5 gt 10"); }
        [TestMethod]
        public void TestParseExpressionLanguagePriceSubGt() { this.TestParseExpressionLanguage("Price sub 5 gt 10"); }
        [TestMethod]
        public void TestParseExpressionLanguagePriceMulGt() { this.TestParseExpressionLanguage("Price mul 2 gt 2000"); }
        [TestMethod]
        public void TestParseExpressionLanguagePriceDivGt() { this.TestParseExpressionLanguage("Price div 2 gt 4"); }
        [TestMethod]
        public void TestParseExpressionLanguagePriceModEq() { this.TestParseExpressionLanguage("Price mod 2 eq 0"); }
        [TestMethod]
        public void TestParseExpressionLanguagePriceSubGe() { this.TestParseExpressionLanguage("Price sub 5 ge 10"); }
        [TestMethod]
        public void TestParseExpressionLanguageBoolOrNotBoolAndNe() { this.TestParseExpressionLanguage("(Bool or not Bool) and 1 ne 2"); }
        [TestMethod]
        public void TestParseExpressionLanguageSubstringofAlfredsCompanyNameEqTrue() { this.TestParseExpressionLanguage("substringof('Alfreds', CompanyName) eq true"); }
        [TestMethod]
        public void TestParseExpressionLanguageEndswithCompanyNameFutterkisteEqTrue() { this.TestParseExpressionLanguage("endswith(CompanyName, 'Futterkiste') eq true"); }
        [TestMethod]
        public void TestParseExpressionLanguageStartswithCompanyNameAlfrEqTrue() { this.TestParseExpressionLanguage("startswith(CompanyName, 'Alfr') eq true"); }
        [TestMethod]
        public void TestParseExpressionLanguageLengthCompanyNameEq() { this.TestParseExpressionLanguage("length(CompanyName) eq 19"); }
        [TestMethod]
        public void TestParseExpressionLanguageIndexofCompanyNameLfredsEq() { this.TestParseExpressionLanguage("indexof(CompanyName, 'lfreds') eq 1"); }
        [TestMethod]
        public void TestParseExpressionLanguageReplaceCompanyNameEqAlfredsFutterkiste() { this.TestParseExpressionLanguage("replace(CompanyName, ' ', '') eq 'AlfredsFutterkiste'"); }
        [TestMethod]
        public void TestParseExpressionLanguageSubstringCompanyNameEqLfredsFutterkiste() { this.TestParseExpressionLanguage("substring(CompanyName, 1) eq 'lfreds Futterkiste'"); }
        [TestMethod]
        public void TestParseExpressionLanguageSubstringCompanyNameEqLf() { this.TestParseExpressionLanguage("substring(CompanyName, 1, 2) eq 'lf'"); }
        [TestMethod]
        public void TestParseExpressionLanguageTolowerCompanyNameEqAlfredsFutterkiste() { this.TestParseExpressionLanguage("tolower(CompanyName) eq 'alfreds futterkiste'"); }
        [TestMethod]
        public void TestParseExpressionLanguageToupperCompanyNameEqALFREDSFUTTERKISTE() { this.TestParseExpressionLanguage("toupper(CompanyName) eq 'ALFREDS FUTTERKISTE'"); }
        [TestMethod]
        public void TestParseExpressionLanguageTrimCompanyNameEqAlfredsFutterkiste() { this.TestParseExpressionLanguage("trim(CompanyName) eq 'Alfreds Futterkiste'"); }
        [TestMethod]
        public void TestParseExpressionLanguageConcatConcatCityCountryEqBerlinGermany() { this.TestParseExpressionLanguage("concat(concat(City, ', '), Country) eq 'Berlin, Germany'"); }
        [TestMethod]
        public void TestParseExpressionLanguageDayBirthDateEq() { this.TestParseExpressionLanguage("day(BirthDate) eq 8"); }
        [TestMethod]
        public void TestParseExpressionLanguageHourBirthDateEq() { this.TestParseExpressionLanguage("hour(BirthDate) eq 0"); }
        [TestMethod]
        public void TestParseExpressionLanguageMinuteBirthDateEq() { this.TestParseExpressionLanguage("minute(BirthDate) eq 0"); }
        [TestMethod]
        public void TestParseExpressionLanguageMonthBirthDateEq() { this.TestParseExpressionLanguage("month(BirthDate) eq 12"); }
        [TestMethod]
        public void TestParseExpressionLanguageSecondBirthDateEq() { this.TestParseExpressionLanguage("second(BirthDate) eq 0"); }
        [TestMethod]
        public void TestParseExpressionLanguageYearBirthDateEq() { this.TestParseExpressionLanguage("year(BirthDate) eq 1948"); }
        [TestMethod]
        public void TestParseExpressionLanguageRoundFreightEq() { this.TestParseExpressionLanguage("round(Freight) eq 32"); }
        [TestMethod]
        public void TestParseExpressionLanguageRoundFreightMEq() { this.TestParseExpressionLanguage("round(FreightM) eq 32"); }
        [TestMethod]
        public void TestParseExpressionLanguageFloorFreightEq() { this.TestParseExpressionLanguage("floor(Freight) eq 32"); }
        [TestMethod]
        public void TestParseExpressionLanguageFloorFreightMEq() { this.TestParseExpressionLanguage("floor(FreightM) eq 32"); }
        [TestMethod]
        public void TestParseExpressionLanguageCeilingFreightEq() { this.TestParseExpressionLanguage("ceiling(Freight) eq 33"); }
        [TestMethod]
        public void TestParseExpressionLanguageCeilingFreightMEq() { this.TestParseExpressionLanguage("ceiling(FreightM) eq 33"); }
        [TestMethod]
        public void TestParseExpressionLanguageIsofMedallionODataTestsODataParsingTestB() { this.TestParseExpressionLanguage("isof('Medallion.OData.Tests.ODataParsingTest+B')"); }
        [TestMethod]
        public void TestParseExpressionLanguageIsofShipCountryEdmString() { this.TestParseExpressionLanguage("isof(ShipCountry, 'Edm.String')"); }

		// from http://www.odata.org/documentation/overview/#AbstractDataModel
        [TestMethod]
        public void TestParseExpressionLanguageNull() { this.TestParseExpressionLanguage("null"); }
        [TestMethod]
        public void TestParseExpressionLanguageTrue() { this.TestParseExpressionLanguage("true"); }
        [TestMethod]
        public void TestParseExpressionLanguageFalse() { this.TestParseExpressionLanguage("false"); }
        [TestMethod]
        public void TestParseExpressionLanguageDatetimeIso() { this.TestParseExpressionLanguage("datetime'2000-12-12T12:00'"); }
        [TestMethod]
        public void TestParseExpressionLanguageM() { this.TestParseExpressionLanguage("2.345M"); }
        [TestMethod]
        public void TestParseExpressionLanguageE() { this.TestParseExpressionLanguage("1E+30"); }
        [TestMethod]
        public void TestParseExpressionLanguageFloat() { this.TestParseExpressionLanguage("2.029"); }
        [TestMethod]
        public void TestParseExpressionLanguageFloat2() { this.TestParseExpressionLanguage("2.1"); }
        [TestMethod]
        public void TestParseExpressionLanguageF() { this.TestParseExpressionLanguage("2.0f"); }
        [TestMethod]
        public void TestParseExpressionLanguageGuidAaaaBbbbCcccDdddeeeeffff() { this.TestParseExpressionLanguage("guid'12345678-aaaa-bbbb-cccc-ddddeeeeffff'"); }
        [TestMethod]
        public void TestParseExpressionLanguageInt() { this.TestParseExpressionLanguage("32"); }
        [TestMethod]
        public void TestParseExpressionLanguageNegativeInt() { this.TestParseExpressionLanguage("-32"); }
        [TestMethod]
        public void TestParseExpressionLanguageL() { this.TestParseExpressionLanguage("64L"); }
        [TestMethod]
        public void TestParseExpressionLanguageNegativeL() { this.TestParseExpressionLanguage("-64L"); }
        [TestMethod]
        public void TestParseExpressionLanguageHelloOData() { this.TestParseExpressionLanguage("'Hello OData'"); }
        //[TestCase("X'23AB", Ignore = true)] // TODO FUTURE not implemented
        //[TestCase("binary'23ABFF'", Ignore = true)] // TODO FUTURE not implemented
        //[TestCase("FF", Ignore = true)] // TODO FUTURE not implemented
        //[TestCase("13:20:00", Ignore = true)] // TODO VNEXT time not implemented
		//[TestCase("datetimeoffset'2002-10-10T17:00:00Z'", Ignore = true)] // TODO VNEXT datetimeoffet not implemente
		
        // my test cases
        [TestMethod]
        public void TestParseExpressionLanguageTextNeNull() { this.TestParseExpressionLanguage("Text ne null"); }
        [TestMethod]
        public void TestParseExpressionLanguageThisStringContainsQuotes() { this.TestParseExpressionLanguage("'this string contains ''quotes'''"); }
        [TestMethod]
        public void TestParseExpressionLanguageDatetimeT() { this.TestParseExpressionLanguage("datetime'2010-01-01T00:00:01'"); }
        [TestMethod]
        public void TestParseExpressionLanguageDatetimeT2() { this.TestParseExpressionLanguage("datetime'2010-01-01T00:00:00.0100000'"); }
        [TestMethod]
        public void TestParseExpressionLanguageCastIntEdmDouble() { this.TestParseExpressionLanguage("cast(Int, 'Edm.Double')"); }
        [TestMethod]
        public void TestParseExpressionLanguageSubAddMul() { this.TestParseExpressionLanguage("1 sub 2 add 3 mul 4"); }
        [TestMethod]
        public void TestParseUnaryPrecedence()
        {
            this.TestParseExpressionLanguage("not Bool and Bool");
            this.TestParseExpressionLanguage("not(Bool and not Bool)");
        }
        [TestMethod]
        public void TestParseMultipleNegation()
        {
            this.TestParseExpressionLanguage("not not not Bool");
        }

		private void TestParseExpressionLanguage(string input)
		{
			var parser = new ODataExpressionLanguageParser(typeof(A), input);
			var parsed = parser.Parse();
			parsed.ToString().ShouldEqual(input);
		}
        #endregion

        #region ---- Sort key parsing ----
        // test from http://www.odata.org/documentation/odata-v2-documentation/uri-conventions/#4_Query_String_Options
        [TestMethod]
        public void TestParseSortKeysInt() { this.TestParseSortKeys("Int", null); }
        [TestMethod]
        public void TestParseSortKeysIntAsc() { this.TestParseSortKeys("Int asc", "Int"); }
        [TestMethod]
        public void TestParseSortKeysIntAddress() { this.TestParseSortKeys("Int,Address/City desc", null); }
		public void TestParseSortKeys(string input, string expected = null)
		{
			var parser = new ODataExpressionLanguageParser(typeof(A), input);
			var parsed = parser.ParseSortKeyList();
			parsed.ToDelimitedString().ShouldEqual(expected ?? input);
		}
        #endregion

        #region ---- Select parsing ----
        [TestMethod]
        public void TestParseSelectStar() { this.TestParseSelect("*"); }
        [TestMethod]
        public void TestParseSelectIntBool() { this.TestParseSelect("Int,Bool"); }
        [TestMethod]
        public void TestParseSelectInner() { this.TestParseSelect("Inner/*,*"); }
        [TestMethod]
        public void TestParseSelectBirthDate() { this.TestParseSelect("BirthDate,Inner/Int,Inner/Bool"); }

		private void TestParseSelect(string input)
		{
			var parser = new ODataExpressionLanguageParser(typeof(A), input);
			var parsed = parser.ParseSelectColumnList();
			parsed.ToDelimitedString().ShouldEqual(input);
		}
        #endregion

        #region ---- Query parsing ----
        [TestMethod]
        public void TestParseQueryTopSkip() { this.TestParseQuery("?$top=1&$skip=5"); }
        [TestMethod]
        public void TestParseQueryFilter() { this.TestParseQuery("?$filter=1+ne+2+and+substringof(Address%2fCity%2c+%27blah%27)"); }
        [TestMethod]
        public void TestParseQuerySelect() { this.TestParseQuery("?$select=Freight%2cBool"); }

		private void TestParseQuery(string query)
		{
			var parsed = ODataQueryParser.Parse(typeof(A), query);
			parsed.ToString().ShouldEqual(query);
		}
        #endregion

        #region ---- Lex tests ----
        [TestMethod]
        public void TestOperatorSpacing() 
        {
           this.TestLex("aeq(eq)", ODataTokenKind.Identifier, ODataTokenKind.LeftParen, ODataTokenKind.Eq, ODataTokenKind.RightParen);
        }

        [TestMethod]
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
