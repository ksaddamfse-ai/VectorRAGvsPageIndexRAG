using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace VectorRAGvsPageIndexRAG.Tools.PdfGenerator;

public class LegalContract
{
    public void Generate(string outputPath)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var boldFont = builder.AddStandard14Font(Standard14Font.HelveticaBold);

        // Title page
        var page = builder.AddPage(PageSize.A4);
        AddText(page, "ENTERPRISE SOFTWARE LICENSE AGREEMENT", 18, boldFont, 50, 750);
        AddText(page, "Effective Date: June 1, 2026", 12, font, 50, 720);
        AddText(page, "Contract No: ESL-2026-0042", 12, font, 50, 700);
        AddText(page, "", 12, font, 50, 680);
        AddText(page, "BETWEEN:", 12, boldFont, 50, 660);
        AddText(page, "CloudSync Inc., a Delaware corporation (\"Licensor\")", 11, font, 70, 640);
        AddText(page, "AND:", 12, boldFont, 50, 620);
        AddText(page, "The entity identified in the Order Form (\"Licensee\")", 11, font, 70, 600);

        // Section 1: Definitions
        page = builder.AddPage(PageSize.A4);
        AddText(page, "SECTION 1: DEFINITIONS", 14, boldFont, 50, 750);
        AddText(page, "1.1 \"Software\" means the CloudSync Enterprise platform, including all modules,", 11, font, 50, 730);
        AddText(page, "    APIs, SDKs, and documentation provided under this Agreement.", 11, font, 70, 715);
        AddText(page, "1.2 \"Authorized User\" means an individual employee or contractor of Licensee", 11, font, 50, 700);
        AddText(page, "    who is authorized to use the Software under the license grant in Section 2.", 11, font, 70, 685);
        AddText(page, "1.3 \"Subscription Term\" means the period specified in the Order Form, typically", 11, font, 50, 670);
        AddText(page, "    twelve (12) months from the Effective Date.", 11, font, 70, 655);
        AddText(page, "1.4 \"Confidential Information\" means all non-public information disclosed by", 11, font, 50, 640);
        AddText(page, "    either party, including but not limited to trade secrets, source code,", 11, font, 70, 625);
        AddText(page, "    customer data, and business strategies.", 11, font, 70, 610);
        AddText(page, "1.5 \"GDPR\" means the General Data Protection Regulation (EU) 2016/679.", 11, font, 50, 595);

        // Section 2: License Grant
        page = builder.AddPage(PageSize.A4);
        AddText(page, "SECTION 2: LICENSE GRANT", 14, boldFont, 50, 750);
        AddText(page, "2.1 Subject to the terms of this Agreement, Licensor grants Licensee a", 11, font, 50, 730);
        AddText(page, "    non-exclusive, non-transferable, revocable license to:", 11, font, 70, 715);
        AddText(page, "    (a) Install and use the Software on Licensee's infrastructure;", 11, font, 70, 700);
        AddText(page, "    (b) Make the Software available to Authorized Users;", 11, font, 70, 685);
        AddText(page, "    (c) Use the Software for Licensee's internal business purposes only.", 11, font, 70, 670);
        AddText(page, "2.2 The license is limited to the number of Authorized Users specified in the", 11, font, 50, 640);
        AddText(page, "    Order Form. Additional users require a separate order.", 11, font, 70, 625);
        AddText(page, "2.3 Licensee may not sublicense, rent, lease, or distribute the Software to", 11, font, 50, 600);
        AddText(page, "    any third party without prior written consent from Licensor.", 11, font, 70, 585);

        // Section 3: Restrictions
        page = builder.AddPage(PageSize.A4);
        AddText(page, "SECTION 3: RESTRICTIONS", 14, boldFont, 50, 750);
        AddText(page, "3.1 Licensee shall not:", 11, font, 50, 730);
        AddText(page, "    (a) Reverse engineer, decompile, or disassemble the Software;", 11, font, 70, 715);
        AddText(page, "    (b) Modify, adapt, or create derivative works of the Software;", 11, font, 70, 700);
        AddText(page, "    (c) Remove or alter any proprietary notices or labels;", 11, font, 70, 685);
        AddText(page, "    (d) Use the Software to provide services to third parties (SaaS);", 11, font, 70, 670);
        AddText(page, "    (e) Exceed the concurrent user limits specified in the Order Form;", 11, font, 70, 655);
        AddText(page, "    (f) Use the Software in violation of applicable laws or regulations.", 11, font, 70, 640);
        AddText(page, "3.2 Licensee shall maintain accurate records of Authorized Users and provide", 11, font, 50, 610);
        AddText(page, "    such records to Licensor upon reasonable request for audit purposes.", 11, font, 70, 595);

        // Section 4: Warranties and Liability
        page = builder.AddPage(PageSize.A4);
        AddText(page, "SECTION 4: WARRANTIES AND LIABILITY", 14, boldFont, 50, 750);
        AddText(page, "4.1 Warranty: Licensor warrants that the Software will perform substantially", 11, font, 50, 730);
        AddText(page, "    in accordance with the documentation for a period of ninety (90) days", 11, font, 70, 715);
        AddText(page, "    from the Effective Date.", 11, font, 70, 700);
        AddText(page, "4.2 LIMITATION OF LIABILITY: IN NO EVENT SHALL EITHER PARTY'S TOTAL AGGREGATE", 11, boldFont, 50, 670);
        AddText(page, "    LIABILITY UNDER THIS AGREEMENT EXCEED THE GREATER OF: (A) THE TOTAL AMOUNTS", 11, boldFont, 70, 655);
        AddText(page, "    PAID BY LICENSEE IN THE TWELVE (12) MONTHS PRECEDING THE CLAIM, OR (B) ONE", 11, boldFont, 70, 640);
        AddText(page, "    HUNDRED THOUSAND DOLLARS ($100,000).", 11, boldFont, 70, 625);
        AddText(page, "4.3 Neither party shall be liable for indirect, incidental, special,", 11, font, 50, 595);
        AddText(page, "    consequential, or punitive damages, including lost profits, data loss,", 11, font, 70, 580);
        AddText(page, "    or business interruption, regardless of the cause of action.", 11, font, 70, 565);
        AddText(page, "4.4 The limitations in Sections 4.2 and 4.3 shall not apply to:", 11, font, 50, 535);
        AddText(page, "    (a) Breach of confidentiality obligations;", 11, font, 70, 520);
        AddText(page, "    (b) Infringement of intellectual property rights;", 11, font, 70, 505);
        AddText(page, "    (c) Gross negligence or willful misconduct.", 11, font, 70, 490);

        // Section 5: Term and Termination
        page = builder.AddPage(PageSize.A4);
        AddText(page, "SECTION 5: TERM AND TERMINATION", 14, boldFont, 50, 750);
        AddText(page, "5.1 This Agreement commences on the Effective Date and continues for the", 11, font, 50, 730);
        AddText(page, "    Subscription Term, unless earlier terminated as provided herein.", 11, font, 70, 715);
        AddText(page, "5.2 Either party may terminate this Agreement:", 11, font, 50, 690);
        AddText(page, "    (a) For cause upon thirty (30) days written notice of a material breach", 11, font, 70, 670);
        AddText(page, "        that remains uncured at the end of such notice period;", 11, font, 70, 655);
        AddText(page, "    (b) Immediately if the other party becomes insolvent, files for bankruptcy,", 11, font, 70, 640);
        AddText(page, "        or makes an assignment for the benefit of creditors;", 11, font, 70, 625);
        AddText(page, "    (c) Upon ninety (90) days written notice for convenience, provided Licensee", 11, font, 70, 610);
        AddText(page, "        has paid all fees due through the end of the notice period.", 11, font, 70, 595);
        AddText(page, "5.3 Upon termination:", 11, font, 50, 565);
        AddText(page, "    (a) All licenses granted hereunder shall immediately cease;", 11, font, 70, 545);
        AddText(page, "    (b) Licensee shall cease all use of the Software and destroy all copies;", 11, font, 70, 530);
        AddText(page, "    (c) Each party shall return or destroy all Confidential Information.", 11, font, 70, 515);
        AddText(page, "5.4 Sections 4, 6, 7, 8, and 9 shall survive termination.", 11, font, 50, 485);

        // Section 6: Intellectual Property
        page = builder.AddPage(PageSize.A4);
        AddText(page, "SECTION 6: INTELLECTUAL PROPERTY", 14, boldFont, 50, 750);
        AddText(page, "6.1 The Software and all intellectual property rights therein are and shall", 11, font, 50, 730);
        AddText(page, "    remain the exclusive property of Licensor. This Agreement does not grant", 11, font, 70, 715);
        AddText(page, "    Licensee any ownership rights in the Software.", 11, font, 70, 700);
        AddText(page, "6.2 Licensee retains all rights in its data. Licensor shall not use Licensee's", 11, font, 50, 670);
        AddText(page, "    data for any purpose other than providing the Services.", 11, font, 70, 655);
        AddText(page, "6.3 Any feedback or suggestions provided by Licensee regarding the Software", 11, font, 50, 630);
        AddText(page, "    may be used by Licensor without restriction or compensation.", 11, font, 70, 615);

        // Section 7: Indemnification
        page = builder.AddPage(PageSize.A4);
        AddText(page, "SECTION 7: INDEMNIFICATION", 14, boldFont, 50, 750);
        AddText(page, "7.1 Licensor shall defend, indemnify, and hold harmless Licensee from and", 11, font, 50, 730);
        AddText(page, "    against any third-party claim that the Software infringes any patent,", 11, font, 70, 715);
        AddText(page, "    copyright, or trade secret of such third party.", 11, font, 70, 700);
        AddText(page, "7.2 Licensee shall defend, indemnify, and hold harmless Licensor from and", 11, font, 50, 670);
        AddText(page, "    against any third-party claim arising from:", 11, font, 70, 655);
        AddText(page, "    (a) Licensee's use of the Software in violation of this Agreement;", 11, font, 70, 640);
        AddText(page, "    (b) Licensee's data or content processed by the Software;", 11, font, 70, 625);
        AddText(page, "    (c) Licensee's violation of applicable laws.", 11, font, 70, 610);
        AddText(page, "7.3 The indemnified party shall: (a) promptly notify the indemnifying party", 11, font, 50, 580);
        AddText(page, "    of the claim; (b) provide reasonable cooperation; and (c) grant sole control", 11, font, 70, 565);
        AddText(page, "    of the defense to the indemnifying party.", 11, font, 70, 550);

        // Section 8: Data Protection and GDPR
        page = builder.AddPage(PageSize.A4);
        AddText(page, "SECTION 8: DATA PROTECTION", 14, boldFont, 50, 750);
        AddText(page, "8.1 Both parties shall comply with all applicable data protection laws,", 11, font, 50, 730);
        AddText(page, "    including but not limited to GDPR, CCPA, and HIPAA where applicable.", 11, font, 70, 715);
        AddText(page, "8.2 For purposes of GDPR:", 11, font, 50, 690);
        AddText(page, "    (a) Licensor acts as a Data Processor;", 11, font, 70, 670);
        AddText(page, "    (b) Licensee acts as a Data Controller;", 11, font, 70, 655);
        AddText(page, "    (c) Licensor shall process personal data only on documented instructions", 11, font, 70, 640);
        AddText(page, "        from Licensee and in accordance with the Data Processing Agreement.", 11, font, 70, 625);
        AddText(page, "8.3 Licensor shall implement appropriate technical and organizational measures", 11, font, 50, 600);
        AddText(page, "    to ensure a level of security appropriate to the risk, including:", 11, font, 70, 585);
        AddText(page, "    (a) Encryption of personal data in transit and at rest;", 11, font, 70, 570);
        AddText(page, "    (b) Regular testing and evaluation of security measures;", 11, font, 70, 555);
        AddText(page, "    (c) Incident response procedures for data breaches;", 11, font, 70, 540);
        AddText(page, "    (d) Annual third-party security audits.", 11, font, 70, 525);
        AddText(page, "8.4 In the event of a personal data breach, Licensor shall notify Licensee", 11, font, 50, 495);
        AddText(page, "    without undue delay and no later than seventy-two (72) hours after", 11, font, 70, 480);
        AddText(page, "    becoming aware of the breach.", 11, font, 70, 465);

        // Section 9: Miscellaneous
        page = builder.AddPage(PageSize.A4);
        AddText(page, "SECTION 9: MISCELLANEOUS", 14, boldFont, 50, 750);
        AddText(page, "9.1 Governing Law: This Agreement shall be governed by the laws of the", 11, font, 50, 730);
        AddText(page, "    State of Delaware, without regard to conflict of laws principles.", 11, font, 70, 715);
        AddText(page, "9.2 Dispute Resolution: Any dispute arising under this Agreement shall be", 11, font, 50, 690);
        AddText(page, "    resolved through binding arbitration under the rules of the American", 11, font, 70, 675);
        AddText(page, "    Arbitration Association in San Francisco, California.", 11, font, 70, 660);
        AddText(page, "9.3 Entire Agreement: This Agreement, together with all Order Forms and", 11, font, 50, 630);
        AddText(page, "    exhibits, constitutes the entire agreement between the parties.", 11, font, 70, 615);
        AddText(page, "9.4 Amendment: No modification of this Agreement shall be effective unless", 11, font, 50, 590);
        AddText(page, "    in writing and signed by authorized representatives of both parties.", 11, font, 70, 575);
        AddText(page, "9.5 Assignment: Neither party may assign this Agreement without the prior", 11, font, 50, 550);
        AddText(page, "    written consent of the other party, except in connection with a merger", 11, font, 70, 535);
        AddText(page, "    or acquisition of substantially all of its assets.", 11, font, 70, 520);
        AddText(page, "9.6 Severability: If any provision of this Agreement is held invalid or", 11, font, 50, 490);
        AddText(page, "    unenforceable, the remaining provisions shall remain in full force.", 11, font, 70, 475);
        AddText(page, "9.7 Notices: All notices shall be in writing and sent to the addresses", 11, font, 50, 450);
        AddText(page, "    specified in the Order Form.", 11, font, 70, 435);

        // Signature block
        page = builder.AddPage(PageSize.A4);
        AddText(page, "SIGNATURES", 14, boldFont, 50, 750);
        AddText(page, "", 12, font, 50, 730);
        AddText(page, "IN WITNESS WHEREOF, the parties have executed this Agreement as of the", 11, font, 50, 710);
        AddText(page, "Effective Date.", 11, font, 50, 695);
        AddText(page, "", 12, font, 50, 670);
        AddText(page, "CLOUDSYNC INC.", 12, boldFont, 50, 650);
        AddText(page, "", 12, font, 50, 630);
        AddText(page, "By: ___________________________", 11, font, 50, 610);
        AddText(page, "Name: John Smith", 11, font, 50, 595);
        AddText(page, "Title: Chief Executive Officer", 11, font, 50, 580);
        AddText(page, "Date: June 1, 2026", 11, font, 50, 565);
        AddText(page, "", 12, font, 50, 540);
        AddText(page, "LICENSEE:", 12, boldFont, 50, 520);
        AddText(page, "", 12, font, 50, 500);
        AddText(page, "By: ___________________________", 11, font, 50, 480);
        AddText(page, "Name: _________________________", 11, font, 50, 465);
        AddText(page, "Title: ________________________", 11, font, 50, 450);
        AddText(page, "Date: _________________________", 11, font, 50, 435);

        var bytes = builder.Build();
        File.WriteAllBytes(outputPath, bytes);
    }

    private static void AddText(PdfPageBuilder page, string text, double fontSize,
        PdfDocumentBuilder.AddedFont font, double x, double y)
    {
        page.AddText(text, fontSize, new PdfPoint(x, y), font);
    }
}
