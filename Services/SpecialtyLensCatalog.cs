namespace VitaMR.Services;

public static class SpecialtyLensCatalog
{
    public static readonly IReadOnlyList<SpecialtyLensDefinition> Lenses =
    [
        new("Neurology", "Neurological conditions, headache, seizure, cognitive symptoms"),
        new("Neurosurgery", "Surgical neuro interventions, craniotomy, spinal surgery"),
        new("Psychiatry_Behavioral", "Mental health, behavioral health, substance use"),
        new("Ophthalmology_Optometry", "Eyes, vision, ocular pressure"),
        new("Otolaryngology_ENT", "Ear, nose, throat, hearing, sinus"),
        new("Dentistry_Maxillofacial", "Dental, jaw, oral surgery"),
        new("Cardiology", "Heart function, arrhythmia, coronary disease, echo findings"),
        new("Cardiovascular_Surgery", "Cardiac surgery and vascular procedures"),
        new("Pulmonology", "Lung function, respiratory symptoms, COPD, asthma, sleep apnea"),
        new("Thoracic_Surgery", "Thoracic procedures and lung surgery"),
        new("Gastroenterology", "GI tract, colonoscopy, IBD, reflux, abdominal symptoms"),
        new("Hepatology", "Liver, hepatitis, cirrhosis, liver function tests"),
        new("Nephrology", "Kidney function, CKD, creatinine, GFR"),
        new("Urology", "Urinary tract, prostate, bladder, renal symptoms"),
        new("Obstetrics_Gynecology", "Reproductive health, pregnancy, gynecological care"),
        new("Orthopedics_SportsMed", "Bone, joint, musculoskeletal, fracture, sports injury"),
        new("Rheumatology", "Autoimmune, arthritis, connective tissue disease"),
        new("Dermatology", "Skin, rash, lesions, wound care"),
        new("Podiatry", "Foot, ankle, diabetic foot"),
        new("Endocrinology", "Hormones, diabetes, thyroid, pituitary, adrenal"),
        new("Infectious_Disease", "Infections, sepsis, HIV, antibiotics"),
        new("Allergy_Immunology", "Allergies, anaphylaxis, immune deficiency"),
        new("Oncology_Hematology", "Cancer, hematologic malignancy, chemotherapy, blood disorders"),
        new("Pathology_Cytology", "Biopsy results, histology, cytology"),
        new("Imaging_Radiology", "CT, MRI, PET, X-ray, ultrasound, imaging reports"),
        new("Labs_Master", "All laboratory results"),
        new("Surgery_Procedures", "Surgical and procedural notes"),
        new("Emergency_CriticalCare", "ER visits, ICU stays, rapid response events"),
        new("PrimaryCare_Preventative", "Routine visits, screening, preventive care, immunizations")
    ];
}

public sealed record SpecialtyLensDefinition(string Name, string Domain);
