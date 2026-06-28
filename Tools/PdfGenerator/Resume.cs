using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace RAGBench.Tools.PdfGenerator;

public class Resume
{
    public void Generate(string outputPath)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var boldFont = builder.AddStandard14Font(Standard14Font.HelveticaBold);

        // Header with email in header area (adversarial case)
        var page = builder.AddPage(PageSize.A4);
        AddText(page, "Dr. Sarah Chen", 24, boldFont, 50, 750);
        AddText(page, "Email: sarah.chen@aimail.com", 11, font, 50, 725);
        AddText(page, "Phone: (555) 123-4567 | LinkedIn: linkedin.com/in/sarahchen", 11, font, 50, 710);
        AddText(page, "GitHub: github.com/sarahchen-ml | Location: San Francisco, CA", 11, font, 50, 695);

        // Summary
        AddText(page, "Professional Summary", 16, boldFont, 50, 660);
        AddText(page, "Senior Machine Learning Engineer with 8+ years of experience in deep learning,", 11, font, 50, 640);
        AddText(page, "natural language processing, and computer vision. Led teams of 5-10 engineers.", 11, font, 50, 625);
        AddText(page, "Published 12 papers in top-tier conferences (NeurIPS, ICML, ACL).", 11, font, 50, 610);
        AddText(page, "Passionate about building production ML systems that scale.", 11, font, 50, 595);

        // Experience - DataCorp
        AddText(page, "Experience", 16, boldFont, 50, 560);
        AddText(page, "Senior ML Engineer | DataCorp Inc. | Jan 2022 - Present", 12, boldFont, 50, 540);
        AddText(page, "Leading the recommendation system team, serving 10M+ daily active users.", 11, font, 70, 520);
        AddText(page, "- Designed and deployed real-time recommendation engine using Python, TensorFlow,", 11, font, 70, 505);
        AddText(page, "  and Apache Kafka, improving click-through rate by 23%", 11, font, 70, 490);
        AddText(page, "- Built A/B testing framework that reduced experiment cycle time from 2 weeks to 3 days", 11, font, 70, 475);
        AddText(page, "- Mentored 4 junior engineers and conducted 50+ technical interviews", 11, font, 70, 460);
        AddText(page, "- Implemented automated model monitoring with Prometheus and Grafana", 11, font, 70, 445);
        AddText(page, "- Technologies: Python, TensorFlow, PyTorch, Spark, AWS, Docker, Kubernetes", 11, font, 70, 430);

        // Experience - AI Labs
        page = builder.AddPage(PageSize.A4);
        AddText(page, "ML Engineer | AI Labs Research | Jun 2019 - Dec 2021", 12, boldFont, 50, 750);
        AddText(page, "Developed NLP models for document understanding and information extraction.", 11, font, 70, 730);
        AddText(page, "- Created BERT-based model for legal document classification (95% accuracy)", 11, font, 70, 715);
        AddText(page, "- Built named entity recognition system for medical records using spaCy", 11, font, 70, 700);
        AddText(page, "- Optimized model inference latency by 40% using TensorRT optimization", 11, font, 70, 685);
        AddText(page, "- Published 3 papers at ACL and EMNLP conferences", 11, font, 70, 670);
        AddText(page, "- Technologies: Python, PyTorch, Hugging Face, spaCy, Java, GCP", 11, font, 70, 655);

        // Experience - TechStart
        AddText(page, "Data Scientist | TechStart AI | Jul 2017 - May 2019", 12, boldFont, 50, 620);
        AddText(page, "Built predictive models for customer churn and lifetime value estimation.", 11, font, 70, 600);
        AddText(page, "- Developed XGBoost model predicting customer churn with 89% AUC", 11, font, 70, 585);
        AddText(page, "- Created dashboard for real-time business metrics using React and D3.js", 11, font, 70, 570);
        AddText(page, "- Automated data pipeline reducing manual reporting by 70%", 11, font, 70, 555);
        AddText(page, "- Technologies: Python, R, SQL, scikit-learn, Tableau, AWS Redshift", 11, font, 70, 540);

        // Education
        page = builder.AddPage(PageSize.A4);
        AddText(page, "Education", 16, boldFont, 50, 750);
        AddText(page, "Ph.D. in Computer Science (Machine Learning)", 12, boldFont, 50, 730);
        AddText(page, "Stanford University | 2017", 11, font, 70, 715);
        AddText(page, "Dissertation: Scalable Methods for Distributed Deep Learning", 11, font, 70, 700);
        AddText(page, "Advisor: Prof. Andrew Ng", 11, font, 70, 685);

        AddText(page, "B.S. in Computer Science & Mathematics", 12, boldFont, 50, 650);
        AddText(page, "MIT | 2013", 11, font, 70, 635);
        AddText(page, "Summa Cum Laude, GPA: 3.95/4.0", 11, font, 70, 620);

        // Skills
        AddText(page, "Technical Skills", 16, boldFont, 50, 580);
        AddText(page, "Programming Languages: Python, Java, C++, R, SQL, JavaScript, Go", 11, font, 70, 560);
        AddText(page, "ML Frameworks: TensorFlow, PyTorch, Keras, scikit-learn, XGBoost, LightGBM", 11, font, 70, 545);
        AddText(page, "NLP Tools: Hugging Face Transformers, spaCy, NLTK, Gensim", 11, font, 70, 530);
        AddText(page, "Cloud Platforms: AWS (SageMaker, EC2, S3), GCP (Vertex AI, BigQuery), Azure ML", 11, font, 70, 515);
        AddText(page, "DevOps: Docker, Kubernetes, CI/CD, Git, Terraform, Prometheus, Grafana", 11, font, 70, 500);
        AddText(page, "Databases: PostgreSQL, MongoDB, Redis, Elasticsearch, Neo4j", 11, font, 70, 485);

        // Publications
        page = builder.AddPage(PageSize.A4);
        AddText(page, "Selected Publications", 16, boldFont, 50, 750);
        AddText(page, "1. Chen et al. (2024) 'Scalable Distributed Training of Large Language Models'", 11, font, 70, 730);
        AddText(page, "   NeurIPS 2024", 11, font, 90, 715);
        AddText(page, "2. Chen, S. et al. (2023) 'Efficient Fine-tuning for Domain-Specific NLP'", 11, font, 70, 695);
        AddText(page, "   ICML 2023", 11, font, 90, 680);
        AddText(page, "3. Chen et al. (2022) 'Real-time Recommendation Systems at Scale'", 11, font, 70, 660);
        AddText(page, "   RecSys 2022", 11, font, 90, 645);
        AddText(page, "4. Chen, S. (2021) 'Legal Document Classification with BERT'", 11, font, 70, 625);
        AddText(page, "   ACL 2021", 11, font, 90, 610);
        AddText(page, "5. Chen et al. (2020) 'Medical NER with Transfer Learning'", 11, font, 70, 590);
        AddText(page, "   EMNLP 2020", 11, font, 90, 575);

        // Certifications
        AddText(page, "Certifications", 16, boldFont, 50, 530);
        AddText(page, "- AWS Machine Learning Specialty (2023)", 11, font, 70, 510);
        AddText(page, "- Google Cloud Professional ML Engineer (2022)", 11, font, 70, 495);
        AddText(page, "- TensorFlow Developer Certificate (2021)", 11, font, 70, 480);

        // Languages
        AddText(page, "Languages", 16, boldFont, 50, 440);
        AddText(page, "English (native), Mandarin (fluent), Spanish (conversational)", 11, font, 70, 420);

        var bytes = builder.Build();
        File.WriteAllBytes(outputPath, bytes);
    }

    private static void AddText(PdfPageBuilder page, string text, double fontSize,
        PdfDocumentBuilder.AddedFont font, double x, double y)
    {
        page.AddText(text, fontSize, new PdfPoint(x, y), font);
    }
}
